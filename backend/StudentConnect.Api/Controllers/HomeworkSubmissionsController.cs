using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using StudentConnect.Infrastructure.Storage;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/homework-submissions")]
public class HomeworkSubmissionsController(
    HomeworkSubmissionStore submissions,
    HomeworkStore homework,
    StudentStore students,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> storageOptions,
    NotificationRecipientResolver recipients,
    INotificationService notifications,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
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

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(submission.StudentId, schoolId))
        {
            return NotFound();
        }

        return Ok(HomeworkSubmissionDto.FromEntity(submission));
    }

    // GET /api/homework-submissions/homework/{homeworkId} — the "who submitted" teacher view.
    // Derived by combining the class roster with submissions, not by storing a "Not Submitted" row.
    [HttpGet("homework/{homeworkId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<SubmissionOverviewDto>>> GetHomeworkSubmissions(int homeworkId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var homeworkItem = await homework.GetByIdAsync(homeworkId, schoolId);
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
                SubmissionId = submission.Id,
                StudentId = student.Id,
                StudentName = $"{student.FirstName} {student.LastName}",
                Status = submission.Status.ToString(),
                SubmittedDate = submission.SubmittedDate,
                Comments = submission.Comments,
                HasFile = submission.StoredFileName is not null,
                TeacherFeedback = submission.TeacherFeedback,
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

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var studentSubmissions = await submissions.GetByStudentAsync(studentId);
        return Ok(studentSubmissions.Select(HomeworkSubmissionDto.FromEntity));
    }

    // POST /api/homework-submissions — the server determines the student from the authenticated
    // caller (never a client-supplied StudentId), plus the timestamp and Submitted/Late status.
    // Multipart/form-data since an attached file is optional alongside the comment.
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<HomeworkSubmissionDto>> CreateSubmission([FromForm] HomeworkSubmissionCreateDto dto)
    {
        if (currentUser.StudentId is not { } studentId)
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var homeworkItem = await homework.GetByIdAsync(dto.HomeworkId, schoolId);
        if (homeworkItem is null)
        {
            return BadRequest(new { message = "Homework does not exist." });
        }

        if (await submissions.ExistsAsync(studentId, dto.HomeworkId))
        {
            return Conflict(new { message = "This student has already submitted this homework." });
        }

        var (fileError, fileName, storedFileName, contentType, fileSize) = await TrySaveAttachmentAsync(dto.File);
        if (fileError is not null)
        {
            return BadRequest(new { message = fileError });
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
                FileName = fileName,
                StoredFileName = storedFileName,
                ContentType = contentType,
                FileSize = fileSize,
            });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save submission for student {StudentId}", studentId);
            if (storedFileName is not null)
            {
                await fileStorage.DeleteFileAsync(storedFileName);
            }
            return StatusCode(500, new { message = "Unable to save the submission. Please try again." });
        }

        var created = await submissions.GetByIdAsync(newId);
        var result = HomeworkSubmissionDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetSubmissionById), new { id = newId }, result);
    }

    // PUT /api/homework-submissions/{id} — the explicit "edit my submission" mechanism; re-evaluates
    // Late status too. Only the owning student may edit their own submission. A new file (if any)
    // replaces the previous one; the old stored file is deleted once the update succeeds.
    [HttpPut("{id:int}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<HomeworkSubmissionDto>> UpdateSubmission(int id, [FromForm] HomeworkSubmissionUpdateDto dto)
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

        var (fileError, fileName, storedFileName, contentType, fileSize) = await TrySaveAttachmentAsync(dto.File);
        if (fileError is not null)
        {
            return BadRequest(new { message = fileError });
        }

        var submittedDate = DateTime.UtcNow;
        var status = DateOnly.FromDateTime(submittedDate) > submission.Homework!.DueDate
            ? HomeworkSubmissionStatus.Late
            : HomeworkSubmissionStatus.Submitted;

        try
        {
            if (storedFileName is not null)
            {
                await submissions.UpdateWithFileAsync(id, submittedDate, status, dto.Comments, fileName!, storedFileName, contentType!, fileSize!.Value);
            }
            else
            {
                await submissions.UpdateAsync(id, submittedDate, status, dto.Comments);
            }
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update submission {Id}", id);
            if (storedFileName is not null)
            {
                await fileStorage.DeleteFileAsync(storedFileName);
            }
            return StatusCode(500, new { message = "Unable to update the submission. Please try again." });
        }

        // Only remove the previous file once the new one is safely recorded, and only if it
        // actually changed — an orphaned file is a smaller problem than losing data.
        if (storedFileName is not null && submission.StoredFileName is not null)
        {
            await fileStorage.DeleteFileAsync(submission.StoredFileName);
        }

        var updated = await submissions.GetByIdAsync(id);
        return Ok(HomeworkSubmissionDto.FromEntity(updated!));
    }

    // GET /api/homework-submissions/{id}/download
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadSubmissionFile(int id)
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

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(submission.StudentId, schoolId))
        {
            return NotFound();
        }

        if (submission.StoredFileName is null)
        {
            return NotFound(new { message = "This submission has no attached file." });
        }

        var stream = await fileStorage.GetFileAsync(submission.StoredFileName);
        if (stream is null)
        {
            return NotFound(new { message = "Submission file is unavailable." });
        }

        return File(stream, submission.ContentType ?? "application/octet-stream", submission.FileName ?? "submission");
    }

    // PUT /api/homework-submissions/{id}/feedback — a teacher's written feedback, distinct from
    // any separately-recorded Mark. Notifies the student and their linked parents.
    [HttpPut("{id:int}/feedback")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<HomeworkSubmissionDto>> AddFeedback(int id, HomeworkSubmissionFeedbackDto dto)
    {
        var submission = await submissions.GetByIdAsync(id);
        if (submission is null)
        {
            return NotFound();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(submission.StudentId, schoolId))
        {
            return NotFound();
        }

        await submissions.UpdateFeedbackAsync(id, dto.TeacherFeedback);

        var recipientUserIds = await recipients.ForStudentAsync(submission.StudentId);
        if (recipientUserIds.Count > 0)
        {
            await notifications.NotifyManyAsync(
                recipientUserIds,
                "Homework feedback added",
                $"{submission.Homework!.Title}: your teacher left new feedback.",
                NotificationType.General);
        }

        var updated = await submissions.GetByIdAsync(id);
        return Ok(HomeworkSubmissionDto.FromEntity(updated!));
    }

    // Validates and saves an optional attachment using the same allow-list/signature-check
    // rules as Learning Materials. Returns a null error and null fields when no file was provided.
    private async Task<(string? Error, string? FileName, string? StoredFileName, string? ContentType, long? FileSize)> TrySaveAttachmentAsync(IFormFile? file)
    {
        if (file is not { Length: > 0 })
        {
            return (null, null, null, null, null);
        }

        if (file.Length > storageOptions.Value.MaxFileSizeBytes)
        {
            return ($"File exceeds the maximum allowed size of {storageOptions.Value.MaxFileSizeBytes / (1024 * 1024)} MB.", null, null, null, null);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFileTypes.IsExtensionAllowed(extension))
        {
            return ("This file type is not supported.", null, null, null, null);
        }

        await using (var checkStream = file.OpenReadStream())
        {
            if (!await AllowedFileTypes.MatchesSignatureAsync(extension, checkStream))
            {
                return ("This file type is not supported.", null, null, null, null);
            }
        }

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        try
        {
            await using var uploadStream = file.OpenReadStream();
            await fileStorage.SaveFileAsync(storedFileName, uploadStream);
        }
        catch (IOException)
        {
            return ("Unable to upload the submission file.", null, null, null, null);
        }

        return (null, file.FileName, storedFileName, file.ContentType, file.Length);
    }
}
