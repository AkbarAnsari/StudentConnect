using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;
using StudentConnect.Api.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarksController(StudentConnectDbContext db, ICurrentUserService currentUser, INotificationService notifications, ILogger<MarksController> logger) : ControllerBase
{
    // GET /api/marks/student/{studentId}
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<MarkRecordDto>>> GetStudentMarks(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var marks = await db.Marks
            .Include(m => m.Assessment).ThenInclude(a => a!.Subject)
            .AsNoTracking()
            .Where(m => m.StudentId == studentId)
            .OrderByDescending(m => m.Assessment!.Date)
            .Select(m => MarkRecordDto.FromEntity(m))
            .ToListAsync();

        return Ok(marks);
    }

    // GET /api/marks/student/{studentId}/subject/{subjectId}
    [HttpGet("student/{studentId:int}/subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<MarkRecordDto>>> GetStudentSubjectMarks(int studentId, int subjectId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var marks = await db.Marks
            .Include(m => m.Assessment).ThenInclude(a => a!.Subject)
            .AsNoTracking()
            .Where(m => m.StudentId == studentId && m.Assessment!.SubjectId == subjectId)
            .OrderByDescending(m => m.Assessment!.Date)
            .Select(m => MarkRecordDto.FromEntity(m))
            .ToListAsync();

        return Ok(marks);
    }

    // GET /api/marks/assessment/{assessmentId}
    [HttpGet("assessment/{assessmentId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<AssessmentResultDto>>> GetAssessmentResults(int assessmentId)
    {
        if (!await db.Assessments.AnyAsync(a => a.Id == assessmentId))
        {
            return NotFound(new { message = "Assessment not found." });
        }

        var results = await db.Marks
            .Include(m => m.Student)
            .AsNoTracking()
            .Where(m => m.AssessmentId == assessmentId)
            .OrderByDescending(m => m.MarksObtained)
            .Select(m => new AssessmentResultDto
            {
                MarkId = m.Id,
                StudentId = m.StudentId,
                StudentName = m.Student!.FirstName + " " + m.Student.LastName,
                MarksObtained = m.MarksObtained,
                MaximumMarks = m.Assessment!.MaximumMarks,
            })
            .ToListAsync();

        return Ok(results);
    }

    // GET /api/marks/student/{studentId}/summary
    [HttpGet("student/{studentId:int}/summary")]
    public async Task<ActionResult<IEnumerable<MarksSummaryDto>>> GetStudentMarksSummary(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var studentMarks = await db.Marks
            .Include(m => m.Assessment).ThenInclude(a => a!.Subject)
            .AsNoTracking()
            .Where(m => m.StudentId == studentId)
            .Select(m => new { m.Assessment!.SubjectId, SubjectName = m.Assessment.Subject!.Name, m.MarksObtained, m.Assessment.MaximumMarks })
            .ToListAsync();

        // Percentage and result category are calculated here, on the backend — never in Angular.
        var summary = studentMarks
            .GroupBy(m => new { m.SubjectId, m.SubjectName })
            .Select(g => new MarksSummaryDto
            {
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.SubjectName,
                Assessments = g.Count(),
                TotalMarksObtained = g.Sum(x => x.MarksObtained),
                TotalMaximumMarks = g.Sum(x => x.MaximumMarks),
            })
            .OrderBy(s => s.SubjectId)
            .ToList();

        foreach (var item in summary)
        {
            item.Percentage = item.TotalMaximumMarks == 0 ? 0 : Math.Round(item.TotalMarksObtained * 100.0 / item.TotalMaximumMarks, 0);
            item.ResultCategory = ResultCategory.From(item.Percentage);
        }

        return Ok(summary);
    }

    // POST /api/marks
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<MarkRecordDto>> CreateMark(MarkCreateDto dto)
    {
        if (!await db.Students.AnyAsync(s => s.Id == dto.StudentId))
        {
            return BadRequest(new { message = "Student does not exist." });
        }

        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.Id == dto.AssessmentId);
        if (assessment is null)
        {
            return BadRequest(new { message = "Assessment does not exist." });
        }

        if (dto.MarksObtained < 0 || dto.MarksObtained > assessment.MaximumMarks)
        {
            return BadRequest(new { message = $"MarksObtained must be between 0 and {assessment.MaximumMarks}." });
        }

        var duplicate = await db.Marks.AnyAsync(m => m.StudentId == dto.StudentId && m.AssessmentId == dto.AssessmentId);
        if (duplicate)
        {
            return Conflict(new { message = "This student already has a mark recorded for this assessment." });
        }

        var mark = new Mark { StudentId = dto.StudentId, AssessmentId = dto.AssessmentId, MarksObtained = dto.MarksObtained };
        db.Marks.Add(mark);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save mark for student {StudentId}", dto.StudentId);
            return StatusCode(500, new { message = "Unable to save the mark. Please try again." });
        }

        mark.Assessment = assessment;
        await db.Entry(assessment).Reference(a => a.Subject).LoadAsync();

        var recipientUserIds = await GetStudentAndParentUserIds(mark.StudentId);
        if (recipientUserIds.Count > 0)
        {
            await notifications.NotifyManyAsync(
                recipientUserIds,
                "New mark recorded",
                $"{assessment.Subject!.Name}: {mark.MarksObtained}/{assessment.MaximumMarks}",
                NotificationType.MarkRecorded);
        }

        var result = MarkRecordDto.FromEntity(mark);
        return CreatedAtAction(nameof(GetStudentMarks), new { studentId = mark.StudentId }, result);
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

        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.Id == dto.AssessmentId);
        if (assessment is null)
        {
            return BadRequest(new { message = "Assessment does not exist." });
        }

        var studentIds = dto.Marks.Select(m => m.StudentId).Distinct().ToList();
        var existingStudentIds = await db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.Id).ToListAsync();
        var missingStudentIds = studentIds.Except(existingStudentIds).ToList();
        if (missingStudentIds.Count > 0)
        {
            return BadRequest(new { message = $"Unknown student id(s): {string.Join(", ", missingStudentIds)}" });
        }

        foreach (var item in dto.Marks)
        {
            if (item.MarksObtained < 0 || item.MarksObtained > assessment.MaximumMarks)
            {
                return BadRequest(new { message = $"MarksObtained for student {item.StudentId} must be between 0 and {assessment.MaximumMarks}." });
            }
        }

        var existing = await db.Marks
            .Where(m => m.AssessmentId == dto.AssessmentId && studentIds.Contains(m.StudentId))
            .ToListAsync();
        var existingByStudent = existing.ToDictionary(m => m.StudentId);

        foreach (var item in dto.Marks)
        {
            if (existingByStudent.TryGetValue(item.StudentId, out var mark))
            {
                mark.MarksObtained = item.MarksObtained;
            }
            else
            {
                db.Marks.Add(new Mark { StudentId = item.StudentId, AssessmentId = dto.AssessmentId, MarksObtained = item.MarksObtained });
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save bulk marks for assessment {AssessmentId}", dto.AssessmentId);
            return StatusCode(500, new { message = "Unable to save marks. Please try again." });
        }

        return Ok(new { message = "Marks saved successfully.", count = dto.Marks.Count });
    }

    // PUT /api/marks/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<MarkRecordDto>> UpdateMark(int id, MarkUpdateDto dto)
    {
        var mark = await db.Marks
            .Include(m => m.Assessment).ThenInclude(a => a!.Subject)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mark is null)
        {
            return NotFound();
        }

        if (dto.MarksObtained < 0 || dto.MarksObtained > mark.Assessment!.MaximumMarks)
        {
            return BadRequest(new { message = $"MarksObtained must be between 0 and {mark.Assessment.MaximumMarks}." });
        }

        mark.MarksObtained = dto.MarksObtained;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update mark {Id}", id);
            return StatusCode(500, new { message = "Unable to update the mark. Please try again." });
        }

        return Ok(MarkRecordDto.FromEntity(mark));
    }

    // DELETE /api/marks/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteMark(int id)
    {
        var mark = await db.Marks.FindAsync(id);
        if (mark is null)
        {
            return NotFound();
        }

        db.Marks.Remove(mark);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete mark {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the mark. Please try again." });
        }

        return NoContent();
    }

    private async Task<List<int>> GetStudentAndParentUserIds(int studentId)
    {
        var studentUserIds = await db.Users
            .Where(u => u.StudentId == studentId)
            .Select(u => u.Id)
            .ToListAsync();

        var parentUserIds = await db.ParentStudents
            .Where(ps => ps.StudentId == studentId)
            .Select(ps => ps.ParentUserId)
            .ToListAsync();

        return studentUserIds.Concat(parentUserIds).Distinct().ToList();
    }
}
