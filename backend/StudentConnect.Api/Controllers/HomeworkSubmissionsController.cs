using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/homework-submissions")]
public class HomeworkSubmissionsController(
    HomeworkSubmissionStore submissions,
    HomeworkStore homework,
    StudentStore students,
    ICurrentUserService currentUser,
    ILogger<HomeworkSubmissionsController> logger) : ControllerBase
{
    // GET /api/homework-submissions/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<HomeworkSubmissionDto>> GetSubmissionById(int id)
    {
        var submission = await submissions.GetByIdAsync(id);

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
        var homeworkItem = await homework.GetByIdAsync(homeworkId);
        if (homeworkItem is null)
        {
            return NotFound(new { message = "Homework not found." });
        }

        var roster = await students.GetByGradeOrderedByFirstNameAsync(homeworkItem.GradeId);
        var homeworkSubmissions = await submissions.GetByHomeworkIdRawAsync(homeworkId);
        var submissionByStudentId = homeworkSubmissions.ToDictionary(s => s.StudentId);

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

        if (!await students.ExistsAsync(studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var studentSubmissions = await submissions.GetByStudentAsync(studentId);
        return Ok(studentSubmissions.Select(HomeworkSubmissionDto.FromEntity));
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

        var homeworkItem = await homework.GetByIdAsync(dto.HomeworkId);
        if (homeworkItem is null)
        {
            return BadRequest(new { message = "Homework does not exist." });
        }

        if (await submissions.ExistsAsync(studentId, dto.HomeworkId))
        {
            return Conflict(new { message = "This student has already submitted this homework." });
        }

        var submittedDate = DateTime.UtcNow;
        var status = DateOnly.FromDateTime(submittedDate) > homeworkItem.DueDate
            ? HomeworkSubmissionStatus.Late
            : HomeworkSubmissionStatus.Submitted;

        int newId;
        try
        {
            newId = await submissions.CreateAsync(new HomeworkSubmission
            {
                HomeworkId = dto.HomeworkId,
                StudentId = studentId,
                SubmittedDate = submittedDate,
                Status = status,
                Comments = dto.Comments,
            });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save submission for student {StudentId}", studentId);
            return StatusCode(500, new { message = "Unable to save the submission. Please try again." });
        }

        var created = await submissions.GetByIdAsync(newId);
        var result = HomeworkSubmissionDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetSubmissionById), new { id = newId }, result);
    }

    // PUT /api/homework-submissions/{id} — the explicit "edit my submission" mechanism; re-evaluates
    // Late status too. Only the owning student may edit their own submission.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<HomeworkSubmissionDto>> UpdateSubmission(int id, HomeworkSubmissionUpdateDto dto)
    {
        var submission = await submissions.GetByIdAsync(id);

        if (submission is null)
        {
            return NotFound();
        }

        if (currentUser.StudentId != submission.StudentId)
        {
            return Forbid();
        }

        var submittedDate = DateTime.UtcNow;
        var status = DateOnly.FromDateTime(submittedDate) > submission.Homework!.DueDate
            ? HomeworkSubmissionStatus.Late
            : HomeworkSubmissionStatus.Submitted;

        try
        {
            await submissions.UpdateAsync(id, submittedDate, status, dto.Comments);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update submission {Id}", id);
            return StatusCode(500, new { message = "Unable to update the submission. Please try again." });
        }

        submission.Comments = dto.Comments;
        submission.SubmittedDate = submittedDate;
        submission.Status = status;
        return Ok(HomeworkSubmissionDto.FromEntity(submission));
    }
}
