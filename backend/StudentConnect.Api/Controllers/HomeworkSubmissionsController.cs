using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/homework-submissions")]
public class HomeworkSubmissionsController(StudentConnectDbContext db, ICurrentUserService currentUser, ILogger<HomeworkSubmissionsController> logger) : ControllerBase
{
    // GET /api/homework-submissions/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HomeworkSubmissionDto>> GetSubmissionById(int id)
    {
        var submission = await db.HomeworkSubmissions
            .Include(s => s.Homework).Include(s => s.Student)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        if (!currentUser.CanAccessStudent(submission.StudentId))
        {
            return Forbid();
        }

        return Ok(HomeworkSubmissionDto.FromEntity(submission));
    }

    // GET /api/homework-submissions/homework/{homeworkId} — the "who submitted" teacher view.
    // Derived by combining the class roster with submissions, not by storing a "Not Submitted" row.
    [HttpGet("homework/{homeworkId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<SubmissionOverviewDto>>> GetHomeworkSubmissions(int homeworkId)
    {
        var homework = await db.Homework.AsNoTracking().FirstOrDefaultAsync(h => h.Id == homeworkId);
        if (homework is null)
        {
            return NotFound(new { message = "Homework not found." });
        }

        var roster = await db.Students
            .AsNoTracking()
            .Where(s => s.GradeId == homework.GradeId)
            .OrderBy(s => s.FirstName)
            .ToListAsync();

        var submissions = await db.HomeworkSubmissions
            .AsNoTracking()
            .Where(s => s.HomeworkId == homeworkId)
            .ToListAsync();
        var submissionByStudentId = submissions.ToDictionary(s => s.StudentId);

        var overview = roster.Select(student => submissionByStudentId.TryGetValue(student.Id, out var submission)
            ? new SubmissionOverviewDto
            {
                StudentId = student.Id,
                StudentName = $"{student.FirstName} {student.LastName}",
                Status = submission.Status.ToString(),
                SubmittedDate = submission.SubmittedDate,
                Comments = submission.Comments,
            }
            : new SubmissionOverviewDto
            {
                StudentId = student.Id,
                StudentName = $"{student.FirstName} {student.LastName}",
                Status = "Not Submitted",
                SubmittedDate = null,
                Comments = null,
            }).ToList();

        return Ok(overview);
    }

    // GET /api/homework-submissions/student/{studentId}
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<HomeworkSubmissionDto>>> GetStudentSubmissions(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var submissions = await db.HomeworkSubmissions
            .Include(s => s.Homework).Include(s => s.Student)
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.SubmittedDate)
            .Select(s => HomeworkSubmissionDto.FromEntity(s))
            .ToListAsync();

        return Ok(submissions);
    }

    // POST /api/homework-submissions — the server determines the student from the authenticated
    // caller (never a client-supplied StudentId), plus the timestamp and Submitted/Late status.
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<HomeworkSubmissionDto>> CreateSubmission(HomeworkSubmissionCreateDto dto)
    {
        if (currentUser.StudentId is not { } studentId)
        {
            return Forbid();
        }

        var homework = await db.Homework.FirstOrDefaultAsync(h => h.Id == dto.HomeworkId);
        if (homework is null)
        {
            return BadRequest(new { message = "Homework does not exist." });
        }

        var duplicate = await db.HomeworkSubmissions.AnyAsync(s => s.StudentId == studentId && s.HomeworkId == dto.HomeworkId);
        if (duplicate)
        {
            return Conflict(new { message = "This student has already submitted this homework." });
        }

        var submittedDate = DateTime.UtcNow;
        var status = DateOnly.FromDateTime(submittedDate) > homework.DueDate
            ? HomeworkSubmissionStatus.Late
            : HomeworkSubmissionStatus.Submitted;

        var submission = new HomeworkSubmission
        {
            HomeworkId = dto.HomeworkId,
            StudentId = studentId,
            SubmittedDate = submittedDate,
            Status = status,
            Comments = dto.Comments,
        };

        db.HomeworkSubmissions.Add(submission);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save submission for student {StudentId}", studentId);
            return StatusCode(500, new { message = "Unable to save the submission. Please try again." });
        }

        submission.Homework = homework;
        await db.Entry(submission).Reference(s => s.Student).LoadAsync();
        var result = HomeworkSubmissionDto.FromEntity(submission);
        return CreatedAtAction(nameof(GetSubmissionById), new { id = submission.Id }, result);
    }

    // PUT /api/homework-submissions/{id} — the explicit "edit my submission" mechanism; re-evaluates
    // Late status too. Only the owning student may edit their own submission.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<HomeworkSubmissionDto>> UpdateSubmission(int id, HomeworkSubmissionUpdateDto dto)
    {
        var submission = await db.HomeworkSubmissions
            .Include(s => s.Homework).Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        if (currentUser.StudentId != submission.StudentId)
        {
            return Forbid();
        }

        var submittedDate = DateTime.UtcNow;
        submission.Comments = dto.Comments;
        submission.SubmittedDate = submittedDate;
        submission.Status = DateOnly.FromDateTime(submittedDate) > submission.Homework!.DueDate
            ? HomeworkSubmissionStatus.Late
            : HomeworkSubmissionStatus.Submitted;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update submission {Id}", id);
            return StatusCode(500, new { message = "Unable to update the submission. Please try again." });
        }

        return Ok(HomeworkSubmissionDto.FromEntity(submission));
    }
}
