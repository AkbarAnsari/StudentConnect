using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarksController(
    MarkStore marks,
    StudentStore students,
    AssessmentStore assessments,
    TermStore terms,
    TeacherSubjectAssignmentStore teacherSubjectAssignments,
    NotificationRecipientResolver recipients,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    GradingBandStore gradingBands,
    INotificationService notifications,
    IAuditService audit,
    Database db,
    ILogger<MarksController> logger) : ControllerBase
{
    // GET /api/marks/student/{studentId}
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<MarkRecordDto>>> GetStudentMarks(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var studentMarks = await marks.GetByStudentAsync(studentId);
        return Ok(studentMarks.Select(MarkRecordDto.FromEntity));
    }

    // GET /api/marks/student/{studentId}/subject/{subjectId}
    [HttpGet("student/{studentId:int}/subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<MarkRecordDto>>> GetStudentSubjectMarks(int studentId, int subjectId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var studentMarks = await marks.GetByStudentAndSubjectAsync(studentId, subjectId);
        return Ok(studentMarks.Select(MarkRecordDto.FromEntity));
    }

    // GET /api/marks/student/{studentId}/summary
    [HttpGet("student/{studentId:int}/summary")]
    public async Task<ActionResult<IEnumerable<MarksSummaryDto>>> GetStudentMarksSummary(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        // Percentage and result category are calculated here, on the backend — never in Angular.
        var summary = await marks.GetSummaryAsync(studentId);
        var bands = await gradingBands.GetForSchoolAsync(schoolId);
        foreach (var item in summary)
        {
            item.ResultCategory = GradingBandStore.Resolve(bands, item.Percentage);
        }
        return Ok(summary);
    }

    // GET /api/marks/assessment/{assessmentId}
    [HttpGet("assessment/{assessmentId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<AssessmentResultDto>>> GetAssessmentResults(int assessmentId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await assessments.ExistsAsync(assessmentId, schoolId))
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var results = await marks.GetAssessmentResultsAsync(assessmentId);
        return Ok(results);
    }

    // POST /api/marks
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<MarkRecordDto>> CreateMark(MarkCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await students.ExistsAsync(dto.StudentId, schoolId))
        {
            return BadRequest(new { message = "Student does not exist." });
        }

        var assessment = await assessments.GetByIdAsync(dto.AssessmentId, schoolId);
        if (assessment is null)
        {
            return BadRequest(new { message = "Assessment does not exist." });
        }

        if (dto.MarksObtained < 0 || dto.MarksObtained > assessment.MaximumMarks)
        {
            return BadRequest(new { message = $"MarksObtained must be between 0 and {assessment.MaximumMarks}." });
        }

        if (assessment.TermId is not null && await terms.IsLockedAsync(assessment.TermId.Value))
        {
            return Conflict(new { message = "This assessment's term is locked; marks can no longer be recorded." });
        }

        if (!await IsAssignedToTeachAsync(assessment.SubjectId, [dto.StudentId]))
        {
            return Forbid();
        }

        if (await marks.ExistsAsync(dto.StudentId, dto.AssessmentId))
        {
            return Conflict(new { message = "This student already has a mark recorded for this assessment." });
        }

        int newId;
        try
        {
            newId = await marks.CreateAsync(dto.StudentId, dto.AssessmentId, dto.MarksObtained);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save mark for student {StudentId}", dto.StudentId);
            return StatusCode(500, new { message = "Unable to save the mark. Please try again." });
        }

        var recipientUserIds = await recipients.ForStudentAsync(dto.StudentId);
        if (recipientUserIds.Count > 0)
        {
            await notifications.NotifyManyAsync(
                recipientUserIds,
                "New mark recorded",
                $"{assessment.Subject!.Name}: {dto.MarksObtained}/{assessment.MaximumMarks}",
                NotificationType.MarkRecorded);
        }

        var created = await marks.GetByIdAsync(newId);
        var result = MarkRecordDto.FromEntity(created!);
        await audit.LogAsync("Create", "Mark", newId.ToString(), $"StudentId={dto.StudentId}, AssessmentId={dto.AssessmentId}, MarksObtained={dto.MarksObtained}");
        return CreatedAtAction(nameof(GetStudentMarks), new { studentId = dto.StudentId }, result);
    }

    // POST /api/marks/bulk — a teacher saving marks for a whole class at once.
    // Existing (student, assessment) marks are corrected in place instead of rejected as duplicates.
    [HttpPost("bulk")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> CreateBulkMarks(BulkMarksRequestDto dto)
    {
        if (dto.Marks.Count == 0)
        {
            return BadRequest(new { message = "At least one mark is required." });
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var assessment = await assessments.GetByIdAsync(dto.AssessmentId, schoolId);
        if (assessment is null)
        {
            return BadRequest(new { message = "Assessment does not exist." });
        }

        if (assessment.TermId is not null && await terms.IsLockedAsync(assessment.TermId.Value))
        {
            return Conflict(new { message = "This assessment's term is locked; marks can no longer be recorded." });
        }

        var studentIds = dto.Marks.Select(m => m.StudentId).Distinct().ToList();
        var missingStudentIds = new List<int>();
        foreach (var studentId in studentIds)
        {
            if (!await students.ExistsAsync(studentId, schoolId))
            {
                missingStudentIds.Add(studentId);
            }
        }
        if (missingStudentIds.Count > 0)
        {
            return BadRequest(new { message = $"Unknown student id(s): {string.Join(", ", missingStudentIds)}" });
        }

        if (!await IsAssignedToTeachAsync(assessment.SubjectId, studentIds))
        {
            return Forbid();
        }

        foreach (var item in dto.Marks)
        {
            if (item.MarksObtained < 0 || item.MarksObtained > assessment.MaximumMarks)
            {
                return BadRequest(new { message = $"MarksObtained for student {item.StudentId} must be between 0 and {assessment.MaximumMarks}." });
            }
        }

        var existingByStudent = await marks.GetExistingIdsForAssessmentAsync(dto.AssessmentId, studentIds);

        try
        {
            await db.WithTransactionAsync(async (connection, tx) =>
            {
                foreach (var item in dto.Marks)
                {
                    if (existingByStudent.TryGetValue(item.StudentId, out var markId))
                    {
                        await marks.UpdateMarksObtainedInTxAsync(connection, tx, markId, item.MarksObtained);
                    }
                    else
                    {
                        await marks.CreateInTxAsync(connection, tx, item.StudentId, dto.AssessmentId, item.MarksObtained);
                    }
                }

                return true;
            });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save bulk marks for assessment {AssessmentId}", dto.AssessmentId);
            return StatusCode(500, new { message = "Unable to save marks. Please try again." });
        }

        await audit.LogAsync("Update", "Mark", $"assessment:{dto.AssessmentId}", $"Bulk save for {dto.Marks.Count} student(s)");
        return Ok(new { message = "Marks saved successfully.", count = dto.Marks.Count });
    }

    // PUT /api/marks/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<MarkRecordDto>> UpdateMark(int id, MarkUpdateDto dto)
    {
        var mark = await marks.GetByIdAsync(id);

        if (mark is null)
        {
            return NotFound();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(mark.StudentId, schoolId))
        {
            return NotFound();
        }

        if (dto.MarksObtained < 0 || dto.MarksObtained > mark.Assessment!.MaximumMarks)
        {
            return BadRequest(new { message = $"MarksObtained must be between 0 and {mark.Assessment.MaximumMarks}." });
        }

        if (mark.Assessment.TermId is not null && await terms.IsLockedAsync(mark.Assessment.TermId.Value))
        {
            return Conflict(new { message = "This mark's term is locked and can no longer be modified." });
        }

        try
        {
            await marks.UpdateMarksObtainedAsync(id, dto.MarksObtained);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update mark {Id}", id);
            return StatusCode(500, new { message = "Unable to update the mark. Please try again." });
        }

        await audit.LogAsync("Update", "Mark", id.ToString(), $"MarksObtained: {mark.MarksObtained} -> {dto.MarksObtained}");
        mark.MarksObtained = dto.MarksObtained;
        return Ok(MarkRecordDto.FromEntity(mark));
    }

    // DELETE /api/marks/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteMark(int id)
    {
        var mark = await marks.GetByIdAsync(id);
        if (mark is null)
        {
            return NotFound();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(mark.StudentId, schoolId))
        {
            return NotFound();
        }

        if (mark.Assessment?.TermId is not null && await terms.IsLockedAsync(mark.Assessment.TermId.Value))
        {
            return Conflict(new { message = "This mark's term is locked and can no longer be deleted." });
        }

        try
        {
            await marks.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete mark {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the mark. Please try again." });
        }

        return NoContent();
    }

    // Admin may write for any section; a Teacher must have an explicit TeacherSubjectAssignment
    // for the subject in every section the target students belong to.
    private async Task<bool> IsAssignedToTeachAsync(int subjectId, IReadOnlyCollection<int> studentIds)
    {
        if (currentUser.Role == "Admin")
        {
            return true;
        }

        if (currentUser.TeacherId is not { } teacherId)
        {
            return false;
        }

        var sectionIds = (await students.GetSectionIdsAsync(studentIds)).Values
            .Where(sectionId => sectionId is not null)
            .Select(sectionId => sectionId!.Value)
            .Distinct();

        foreach (var sectionId in sectionIds)
        {
            if (!await teacherSubjectAssignments.ExistsAsync(teacherId, subjectId, sectionId))
            {
                return false;
            }
        }

        return true;
    }
}
