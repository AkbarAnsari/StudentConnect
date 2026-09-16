using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/homework")]
public class HomeworksController(
    HomeworkStore homework,
    HomeworkSubmissionStore submissions,
    StudentStore students,
    ReferenceDataStore referenceData,
    TeacherSubjectAssignmentStore assignments,
    NotificationRecipientResolver recipients,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    INotificationService notifications,
    ILogger<HomeworksController> logger) : ControllerBase
{
    // GET /api/homework
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetHomework()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await homework.GetAllAsync(schoolId);
        if (currentUser.Role == "Teacher") all = all.Where(item => item.TeacherId == currentUser.TeacherId).ToList();
        return Ok(all.Select(HomeworkDto.FromEntity));
    }

    // GET /api/homework/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HomeworkDto>> GetHomeworkById(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var item = await homework.GetByIdAsync(id, schoolId);

        if (item is null)
        {
            return NotFound();
        }

        if (!await CanReadHomeworkAsync(item, schoolId))
        {
            return Forbid();
        }

        return Ok(HomeworkDto.FromEntity(item));
    }

    // GET /api/homework/student/{studentId} — status is derived from the presence (or absence) of a submission.
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<StudentHomeworkDto>>> GetStudentHomework(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var student = await students.GetByIdAsync(studentId, schoolId);
        if (student is null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var gradeHomework = await homework.GetForStudentAsync(student.GradeId, student.SectionId, schoolId);
        var homeworkIds = gradeHomework.Select(h => h.Id).ToList();
        var studentSubmissions = await submissions.GetByStudentForHomeworkIdsAsync(studentId, homeworkIds);
        var submissionByHomeworkId = studentSubmissions.ToDictionary(s => s.HomeworkId);

        var result = gradeHomework.Select(h => new StudentHomeworkDto
        {
            Id = h.Id,
            Title = h.Title,
            SubjectName = h.Subject?.Name ?? string.Empty,
            DueDate = h.DueDate,
            Status = submissionByHomeworkId.TryGetValue(h.Id, out var submission) ? submission.Status.ToString() : "Not Submitted",
        }).ToList();

        return Ok(result);
    }

    // GET /api/homework/teacher/{teacherId}
    [HttpGet("teacher/{teacherId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetTeacherHomework(int teacherId)
    {
        if (!currentUser.CanActAsTeacher(teacherId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.TeacherExistsAsync(teacherId, schoolId))
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var byTeacher = await homework.GetByTeacherAsync(teacherId, schoolId);
        return Ok(byTeacher.Select(HomeworkDto.FromEntity));
    }

    // GET /api/homework/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetSubjectHomework(int subjectId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.SubjectExistsAsync(subjectId, schoolId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var bySubject = await homework.GetBySubjectAsync(subjectId, schoolId);
        if (currentUser.Role == "Teacher") bySubject = bySubject.Where(item => item.TeacherId == currentUser.TeacherId).ToList();
        return Ok(bySubject.Select(HomeworkDto.FromEntity));
    }

    // POST /api/homework
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<HomeworkDto>> CreateHomework(HomeworkCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (!await referenceData.SubjectExistsAsync(dto.SubjectId, schoolId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await referenceData.GradeExistsAsync(dto.GradeId, schoolId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (!await referenceData.TeacherExistsAsync(dto.TeacherId, schoolId))
        {
            return BadRequest(new { message = "Teacher does not exist." });
        }

        if (!await assignments.ExistsAsync(dto.TeacherId, dto.SubjectId, dto.SectionId, schoolId))
        {
            return BadRequest(new { message = "The selected Teacher is not assigned to this subject and section." });
        }

        if (!currentUser.CanActAsTeacher(dto.TeacherId))
        {
            return Forbid();
        }

        if (!TryResolveInitialStatus(dto.Status, dto.ScheduledForUtc, out var lifecycle, out var lifecycleError))
        {
            return BadRequest(new { message = lifecycleError });
        }

        var newHomework = new Homework
        {
            Title = dto.Title,
            Description = dto.Description,
            SubjectId = dto.SubjectId,
            GradeId = dto.GradeId,
            SectionId = dto.SectionId,
            TeacherId = dto.TeacherId,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = dto.DueDate,
            Status = lifecycle,
            ScheduledForUtc = lifecycle == ContentLifecycleStatus.Scheduled ? dto.ScheduledForUtc : null,
            PublishedAtUtc = lifecycle == ContentLifecycleStatus.Published ? DateTime.UtcNow : null,
        };

        int newId;
        try
        {
            newId = await homework.CreateAsync(newHomework);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save homework {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the homework. Please try again." });
        }

        var created = await homework.GetByIdAsync(newId, schoolId);

        if (lifecycle == ContentLifecycleStatus.Published)
        {
            await NotifyHomeworkReleasedAsync(created!, schoolId);
        }

        var result = HomeworkDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetHomeworkById), new { id = newId }, result);
    }

    // PUT /api/homework/{id} — only Title, Description, and DueDate can be corrected; Subject/Grade/Teacher stay fixed.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<HomeworkDto>> UpdateHomework(int id, HomeworkUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var existing = await homework.GetByIdAsync(id, schoolId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!currentUser.CanActAsTeacher(existing.TeacherId))
        {
            return Forbid();
        }

        if (existing.Status != ContentLifecycleStatus.Draft)
        {
            return Conflict(new { message = "Reopen this homework to Draft before editing it." });
        }

        try
        {
            await homework.UpdateAsync(id, dto.Title, dto.Description, dto.DueDate);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update homework {Id}", id);
            return StatusCode(500, new { message = "Unable to update the homework. Please try again." });
        }

        var updated = await homework.GetByIdAsync(id, schoolId);
        return Ok(HomeworkDto.FromEntity(updated!));
    }

    // PUT /api/homework/bulk-due-date — extend/correct the deadline across several of the
    // caller's own homework items at once (Admin may act on any).
    [HttpPut("bulk-due-date")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> BulkUpdateDueDate(HomeworkBulkDueDateDto dto)
    {
        if (dto.HomeworkIds.Count == 0)
        {
            return BadRequest(new { message = "At least one homework id is required." });
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        foreach (var id in dto.HomeworkIds)
        {
            var item = await homework.GetByIdAsync(id, schoolId);
            if (item is null)
            {
                return BadRequest(new { message = $"Homework {id} does not exist." });
            }

            if (!currentUser.CanActAsTeacher(item.TeacherId))
            {
                return Forbid();
            }

            if (item.Status != ContentLifecycleStatus.Draft)
            {
                return Conflict(new { message = $"Reopen '{item.Title}' to Draft before changing its due date." });
            }
        }

        try
        {
            var updated = await homework.UpdateDueDatesAsync(dto.HomeworkIds, dto.DueDate);
            return Ok(new { message = "Due date updated.", count = updated });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to bulk-update due dates for {Count} homework item(s)", dto.HomeworkIds.Count);
            return StatusCode(500, new { message = "Unable to update due dates. Please try again." });
        }
    }

    // DELETE /api/homework/{id} — related submissions are removed with it (they have no meaning without the assignment).
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteHomework(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var existing = await homework.GetByIdAsync(id, schoolId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!currentUser.CanActAsTeacher(existing.TeacherId))
        {
            return Forbid();
        }

        if (existing.Status != ContentLifecycleStatus.Draft)
        {
            return Conflict(new { message = "Only Draft homework can be permanently deleted. Archive released homework instead." });
        }

        try
        {
            await homework.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete homework {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the homework. Please try again." });
        }

        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<HomeworkDto>> PublishHomework(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Published);

    [HttpPost("{id:int}/schedule")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<HomeworkDto>> ScheduleHomework(int id, ContentScheduleDto dto) =>
        dto.ScheduledForUtc <= DateTime.UtcNow
            ? Task.FromResult<ActionResult<HomeworkDto>>(BadRequest(new { message = "Scheduled time must be in the future." }))
            : ChangeLifecycleAsync(id, ContentLifecycleStatus.Scheduled, dto.ScheduledForUtc);

    [HttpPost("{id:int}/close")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<HomeworkDto>> CloseHomework(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Closed);

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<HomeworkDto>> ArchiveHomework(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Archived);

    [HttpPost("{id:int}/reopen")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<HomeworkDto>> ReopenHomework(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Draft);

    private async Task<bool> CanReadHomeworkAsync(Homework item, int schoolId)
    {
        if (currentUser.IsTeacherOrAdmin)
        {
            return true;
        }

        if (!IsLearnerVisible(item))
        {
            return false;
        }

        if (currentUser.StudentId is { } studentId)
        {
            return await StudentCanReadHomeworkAsync(studentId, item, schoolId);
        }

        foreach (var childStudentId in currentUser.ChildStudentIds)
        {
            if (await StudentCanReadHomeworkAsync(childStudentId, item, schoolId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> StudentCanReadHomeworkAsync(int studentId, Homework item, int schoolId)
    {
        var student = await students.GetByIdAsync(studentId, schoolId);
        if (student?.GradeId != item.GradeId || (item.SectionId is not null && student.SectionId != item.SectionId))
        {
            return false;
        }

        return (await students.GetSubjectIdsForStudentAsync(studentId)).Contains(item.SubjectId);
    }

    private async Task<ActionResult<HomeworkDto>> ChangeLifecycleAsync(
        int id,
        ContentLifecycleStatus status,
        DateTime? scheduledForUtc = null)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var item = await homework.GetByIdAsync(id, schoolId);
        if (item is null)
        {
            return NotFound();
        }

        if (!currentUser.CanActAsTeacher(item.TeacherId))
        {
            return Forbid();
        }

        await homework.SetLifecycleAsync(id, status, scheduledForUtc);
        var updated = (await homework.GetByIdAsync(id, schoolId))!;
        if (status == ContentLifecycleStatus.Published && item.Status != ContentLifecycleStatus.Published)
        {
            await NotifyHomeworkReleasedAsync(updated, schoolId);
        }

        return Ok(HomeworkDto.FromEntity(updated));
    }

    private async Task NotifyHomeworkReleasedAsync(Homework item, int schoolId)
    {
        var studentIds = item.SectionId is { } sectionId
            ? (await students.GetActiveBySectionAndSubjectAsync(sectionId, item.SubjectId, schoolId)).Select(student => student.Id).ToList()
            : (await students.GetActiveByGradeAndSubjectAsync(item.GradeId, item.SubjectId, schoolId)).Select(student => student.Id).ToList();
        foreach (var studentId in studentIds)
        {
            var recipientUserIds = await recipients.ForStudentAsync(studentId);
            await notifications.NotifyManyForHomeworkAsync(
                recipientUserIds,
                "New homework assigned",
                $"{item.Subject!.Name}: {item.Title} (due {item.DueDate:yyyy-MM-dd})",
                NotificationType.HomeworkAssigned,
                $"/students/{studentId}/homework/{item.Id}",
                item.Id);
        }
    }

    private static bool IsLearnerVisible(Homework item) =>
        item.Status is ContentLifecycleStatus.Published or ContentLifecycleStatus.Closed ||
        item.Status == ContentLifecycleStatus.Scheduled && item.ScheduledForUtc <= DateTime.UtcNow;

    private static bool TryResolveInitialStatus(
        string value,
        DateTime? scheduledForUtc,
        out ContentLifecycleStatus status,
        out string? error)
    {
        if (!Enum.TryParse(value, true, out status) || status is ContentLifecycleStatus.Closed or ContentLifecycleStatus.Archived)
        {
            error = "Status must be Draft, Scheduled, or Published.";
            return false;
        }

        if (status == ContentLifecycleStatus.Scheduled && (scheduledForUtc is null || scheduledForUtc <= DateTime.UtcNow))
        {
            error = "A future scheduled time is required.";
            return false;
        }

        error = null;
        return true;
    }
}
