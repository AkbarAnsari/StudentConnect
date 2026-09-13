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
    NotificationRecipientResolver recipients,
    ICurrentUserService currentUser,
    INotificationService notifications,
    ILogger<HomeworksController> logger) : ControllerBase
{
    // GET /api/homework
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetHomework()
    {
        var all = await homework.GetAllAsync();
        return Ok(all.Select(HomeworkDto.FromEntity));
    }

    // GET /api/homework/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HomeworkDto>> GetHomeworkById(int id)
    {
        var item = await homework.GetByIdAsync(id);

        if (item is null)
        {
            return NotFound();
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

        var student = await students.GetByIdAsync(studentId);
        if (student is null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var gradeHomework = await homework.GetByGradeAsync(student.GradeId);
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

        if (!await referenceData.TeacherExistsAsync(teacherId))
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var byTeacher = await homework.GetByTeacherAsync(teacherId);
        return Ok(byTeacher.Select(HomeworkDto.FromEntity));
    }

    // GET /api/homework/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetSubjectHomework(int subjectId)
    {
        if (!await referenceData.SubjectExistsAsync(subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var bySubject = await homework.GetBySubjectAsync(subjectId);
        return Ok(bySubject.Select(HomeworkDto.FromEntity));
    }

    // POST /api/homework
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<HomeworkDto>> CreateHomework(HomeworkCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (!await referenceData.SubjectExistsAsync(dto.SubjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await referenceData.GradeExistsAsync(dto.GradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (!await referenceData.TeacherExistsAsync(dto.TeacherId))
        {
            return BadRequest(new { message = "Teacher does not exist." });
        }

        if (!currentUser.CanActAsTeacher(dto.TeacherId))
        {
            return Forbid();
        }

        var newHomework = new Homework
        {
            Title = dto.Title,
            Description = dto.Description,
            SubjectId = dto.SubjectId,
            GradeId = dto.GradeId,
            TeacherId = dto.TeacherId,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = dto.DueDate,
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

        var created = await homework.GetByIdAsync(newId);

        var studentIds = await homework.GetAllStudentIdsInGradeAsync(dto.GradeId);
        var recipientUserIds = await recipients.ForStudentsAsync(studentIds);
        if (recipientUserIds.Count > 0)
        {
            await notifications.NotifyManyAsync(
                recipientUserIds,
                "New homework assigned",
                $"{created!.Subject!.Name}: {created.Title} (due {created.DueDate:yyyy-MM-dd})",
                NotificationType.HomeworkAssigned);
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

        if (!await homework.ExistsAsync(id))
        {
            return NotFound();
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

        var updated = await homework.GetByIdAsync(id);
        return Ok(HomeworkDto.FromEntity(updated!));
    }

    // DELETE /api/homework/{id} — related submissions are removed with it (they have no meaning without the assignment).
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteHomework(int id)
    {
        if (!await homework.ExistsAsync(id))
        {
            return NotFound();
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
}
