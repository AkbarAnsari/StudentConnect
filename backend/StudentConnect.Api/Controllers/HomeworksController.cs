using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;
using StudentConnect.Api.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/homework")]
public class HomeworksController(StudentConnectDbContext db, ICurrentUserService currentUser, INotificationService notifications, ILogger<HomeworksController> logger) : ControllerBase
{
    // GET /api/homework
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetHomework()
    {
        var homework = await db.Homework
            .Include(h => h.Subject).Include(h => h.Grade).Include(h => h.Teacher)
            .AsNoTracking()
            .OrderBy(h => h.DueDate)
            .Select(h => HomeworkDto.FromEntity(h))
            .ToListAsync();

        return Ok(homework);
    }

    // GET /api/homework/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HomeworkDto>> GetHomeworkById(int id)
    {
        var homework = await db.Homework
            .Include(h => h.Subject).Include(h => h.Grade).Include(h => h.Teacher)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id);

        if (homework is null)
        {
            return NotFound();
        }

        return Ok(HomeworkDto.FromEntity(homework));
    }

    // GET /api/homework/student/{studentId} — status is derived from the presence (or absence) of a submission.
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<StudentHomeworkDto>>> GetStudentHomework(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var homework = await db.Homework
            .Include(h => h.Subject)
            .AsNoTracking()
            .Where(h => h.GradeId == student.GradeId)
            .OrderBy(h => h.DueDate)
            .ToListAsync();

        var homeworkIds = homework.Select(h => h.Id).ToList();
        var submissions = await db.HomeworkSubmissions
            .AsNoTracking()
            .Where(s => s.StudentId == studentId && homeworkIds.Contains(s.HomeworkId))
            .ToListAsync();
        var submissionByHomeworkId = submissions.ToDictionary(s => s.HomeworkId);

        var result = homework.Select(h => new StudentHomeworkDto
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

        if (!await db.Teachers.AnyAsync(t => t.Id == teacherId))
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var homework = await db.Homework
            .Include(h => h.Subject).Include(h => h.Grade).Include(h => h.Teacher)
            .AsNoTracking()
            .Where(h => h.TeacherId == teacherId)
            .OrderBy(h => h.DueDate)
            .Select(h => HomeworkDto.FromEntity(h))
            .ToListAsync();

        return Ok(homework);
    }

    // GET /api/homework/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<HomeworkDto>>> GetSubjectHomework(int subjectId)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var homework = await db.Homework
            .Include(h => h.Subject).Include(h => h.Grade).Include(h => h.Teacher)
            .AsNoTracking()
            .Where(h => h.SubjectId == subjectId)
            .OrderBy(h => h.DueDate)
            .Select(h => HomeworkDto.FromEntity(h))
            .ToListAsync();

        return Ok(homework);
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

        if (!await db.Subjects.AnyAsync(s => s.Id == dto.SubjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await db.Grades.AnyAsync(g => g.Id == dto.GradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (!await db.Teachers.AnyAsync(t => t.Id == dto.TeacherId))
        {
            return BadRequest(new { message = "Teacher does not exist." });
        }

        if (!currentUser.CanActAsTeacher(dto.TeacherId))
        {
            return Forbid();
        }

        var homework = new Homework
        {
            Title = dto.Title,
            Description = dto.Description,
            SubjectId = dto.SubjectId,
            GradeId = dto.GradeId,
            TeacherId = dto.TeacherId,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = dto.DueDate,
        };

        db.Homework.Add(homework);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save homework {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the homework. Please try again." });
        }

        await db.Entry(homework).Reference(h => h.Subject).LoadAsync();
        await db.Entry(homework).Reference(h => h.Grade).LoadAsync();
        await db.Entry(homework).Reference(h => h.Teacher).LoadAsync();

        var studentIds = await db.Students.Where(s => s.GradeId == homework.GradeId).Select(s => s.Id).ToListAsync();
        var studentUserIds = await db.Users.Where(u => u.StudentId != null && studentIds.Contains(u.StudentId!.Value)).Select(u => u.Id).ToListAsync();
        var parentUserIds = await db.ParentStudents.Where(ps => studentIds.Contains(ps.StudentId)).Select(ps => ps.ParentUserId).ToListAsync();
        var recipientUserIds = studentUserIds.Concat(parentUserIds).Distinct().ToList();
        if (recipientUserIds.Count > 0)
        {
            await notifications.NotifyManyAsync(
                recipientUserIds,
                "New homework assigned",
                $"{homework.Subject!.Name}: {homework.Title} (due {homework.DueDate:yyyy-MM-dd})",
                NotificationType.HomeworkAssigned);
        }

        var result = HomeworkDto.FromEntity(homework);
        return CreatedAtAction(nameof(GetHomeworkById), new { id = homework.Id }, result);
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

        var homework = await db.Homework
            .Include(h => h.Subject).Include(h => h.Grade).Include(h => h.Teacher)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (homework is null)
        {
            return NotFound();
        }

        homework.Title = dto.Title;
        homework.Description = dto.Description;
        homework.DueDate = dto.DueDate;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update homework {Id}", id);
            return StatusCode(500, new { message = "Unable to update the homework. Please try again." });
        }

        return Ok(HomeworkDto.FromEntity(homework));
    }

    // DELETE /api/homework/{id} — related submissions are removed with it (they have no meaning without the assignment).
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteHomework(int id)
    {
        var homework = await db.Homework.FindAsync(id);
        if (homework is null)
        {
            return NotFound();
        }

        db.Homework.Remove(homework);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete homework {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the homework. Please try again." });
        }

        return NoContent();
    }
}
