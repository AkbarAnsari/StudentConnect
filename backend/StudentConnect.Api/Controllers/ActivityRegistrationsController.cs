using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using StudentConnect.Infrastructure.Storage;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/activity-registrations")]
public class ActivityRegistrationsController(
    ActivityRegistrationStore registrations,
    StudentStore students,
    ActivityStore activities,
    Database db,
    NotificationRecipientResolver recipients,
    INotificationService notifications,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    ILogger<ActivityRegistrationsController> logger,
    IFileStorage? fileStorage = null,
    IOptions<FileStorageOptions>? storageOptions = null) : ControllerBase
{
    // GET /api/activity-registrations/student/{studentId}
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<ActivityRegistrationDto>>> GetStudentRegistrations(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var byStudent = await registrations.GetByStudentAsync(studentId);
        return Ok(byStudent.Select(ActivityRegistrationDto.FromEntity));
    }

    // GET /api/activity-registrations/activity/{activityId}
    [HttpGet("activity/{activityId:int}")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<IEnumerable<ActivityRegistrationDto>>> GetActivityRegistrations(int activityId)
    {
        if (currentTenant.SchoolId is not { } schoolId || await activities.GetByIdAsync(activityId, schoolId) is not { } activity)
        {
            return NotFound(new { message = "Activity not found." });
        }
        if (!CanManage(activity)) return Forbid();

        var byActivity = await registrations.GetByActivityAsync(activityId);
        return Ok(byActivity.Select(ActivityRegistrationDto.FromEntity));
    }

    // GET /api/activity-registrations/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityRegistrationDto>> GetRegistrationById(int id)
    {
        var registration = await registrations.GetByIdAsync(id);

        if (registration is null)
        {
            return NotFound();
        }

        if (!currentUser.CanAccessStudent(registration.StudentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(registration.StudentId, schoolId))
        {
            return NotFound();
        }

        if (currentUser.IsTeacherOrAdmin)
        {
            var activity = await activities.GetByIdAsync(registration.ActivityId, schoolId);
            if (activity is null) return NotFound();
            if (!CanManage(activity)) return Forbid();
        }

        return Ok(ActivityRegistrationDto.FromEntity(registration));
    }

    // POST /api/activity-registrations — the server determines the student from the authenticated
    // caller (never a client-supplied StudentId); a client cannot register a different student.
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<ActivityRegistrationDto>> Register(ActivityRegistrationCreateDto dto)
    {
        if (currentUser.StudentId is not { } studentId)
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
            return BadRequest(new { message = "Student does not exist." });
        }

        var activity = await activities.GetByIdAsync(dto.ActivityId, schoolId);
        if (activity is null)
        {
            return BadRequest(new { message = "Activity does not exist." });
        }

        var now = DateTime.UtcNow;
        if (activity.Status != ActivityStatus.Open)
        {
            return BadRequest(new { message = "Registration is not open for this activity." });
        }

        if (activity.Date < now || activity.RegistrationClosesAtUtc <= now)
        {
            return BadRequest(new { message = "Registration is closed for this activity." });
        }

        if (activity.RegistrationOpensAtUtc > now)
        {
            return BadRequest(new { message = "Registration has not opened yet." });
        }

        if (student.GradeId != activity.GradeId)
        {
            return BadRequest(new { message = "Student is not eligible for this activity." });
        }

        if (activity.SectionId is not null && student.SectionId != activity.SectionId)
        {
            return BadRequest(new { message = "Student is not eligible for this section activity." });
        }

        if (activity.ConsentRequired && !dto.ConsentConfirmed)
        {
            return BadRequest(new { message = "Consent must be confirmed before registration." });
        }

        // A transaction narrows (without fully eliminating) the race where two near-simultaneous
        // requests both read a capacity count that's about to become stale — a stronger guarantee
        // (e.g. serializable isolation or a concurrency token) would be needed for a highly
        // concurrent production system; that's out of scope for this learning project.
        // An activity at capacity waitlists the student instead of rejecting them outright.
        int registrationId;
        var registrationDate = DateTime.UtcNow;
        try
        {
            var (Id, AlreadyRegistered) = await db.WithTransactionAsync(async (connection, tx) =>
            {
                var (existingId, existingStatus) = await registrations.GetExistingInTxAsync(connection, tx, studentId, dto.ActivityId, schoolId);

                if (existingId is not null && existingStatus is ActivityRegistrationStatus.Registered or ActivityRegistrationStatus.Waitlisted)
                {
                    return (existingId.Value, true);
                }

                var currentRegistrations = await registrations.CountActiveInTxAsync(connection, tx, dto.ActivityId, schoolId);
                var targetStatus = currentRegistrations >= activity.Capacity
                    ? ActivityRegistrationStatus.Waitlisted
                    : ActivityRegistrationStatus.Registered;

                if (existingId is not null)
                {
                    await registrations.ReactivateInTxAsync(connection, tx, existingId.Value, registrationDate, targetStatus, schoolId, dto.ConsentConfirmed);
                    return (existingId.Value, false);
                }

                var newId = await registrations.InsertInTxAsync(connection, tx, studentId, dto.ActivityId, registrationDate, targetStatus, schoolId, dto.ConsentConfirmed);
                return (newId ?? 0, false);
            });

            if (AlreadyRegistered)
            {
                return Conflict(new { message = "Student is already registered for this activity." });
            }

            if (Id == 0)
            {
                return BadRequest(new { message = "Activity does not exist." });
            }

            registrationId = Id;
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save registration for student {StudentId} on activity {ActivityId}", studentId, dto.ActivityId);
            return StatusCode(500, new { message = "Unable to register for this activity. Please try again." });
        }

        var created = await registrations.GetByIdAsync(registrationId);
        var result = ActivityRegistrationDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetRegistrationById), new { id = registrationId }, result);
    }

    // PUT /api/activity-registrations/{id}/cancel — Status flips to Cancelled; the row is kept as history.
    // The owning student (or a Teacher/Admin) may cancel a registration. Freeing a Registered seat
    // promotes the earliest Waitlisted registration for the same activity, if any.
    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<ActivityRegistrationDto>> CancelRegistration(int id)
    {
        var registration = await registrations.GetByIdAsync(id);

        if (registration is null)
        {
            return NotFound();
        }

        if (!currentUser.CanAccessStudent(registration.StudentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(registration.StudentId, schoolId))
        {
            return NotFound();
        }

        if (currentUser.IsTeacherOrAdmin)
        {
            var activity = await activities.GetByIdAsync(registration.ActivityId, schoolId);
            if (activity is null) return NotFound();
            if (!CanManage(activity)) return Forbid();
        }

        var freedASeat = registration.Status == ActivityRegistrationStatus.Registered;
        int? promotedRegistrationId = null;

        try
        {
            promotedRegistrationId = await db.WithTransactionAsync(async (connection, tx) =>
            {
                await registrations.UpdateStatusInTxAsync(connection, tx, id, ActivityRegistrationStatus.Cancelled, schoolId);

                if (!freedASeat)
                {
                    return (int?)null;
                }

                if (await registrations.GetEarliestWaitlistedInTxAsync(connection, tx, registration.ActivityId, schoolId) is not { } nextId)
                {
                    return null;
                }

                await registrations.PromoteInTxAsync(connection, tx, nextId, schoolId);
                return nextId;
            });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to cancel registration {Id}", id);
            return StatusCode(500, new { message = "Unable to cancel the registration. Please try again." });
        }

        if (promotedRegistrationId is { } promotedId)
        {
            var promoted = await registrations.GetByIdAsync(promotedId);
            if (promoted is not null)
            {
                var recipientUserIds = await recipients.ForStudentAsync(promoted.StudentId);
                if (recipientUserIds.Count > 0)
                {
                    await notifications.NotifyManyAsync(
                        recipientUserIds,
                        "Moved off the waitlist",
                        $"A seat opened up for {promoted.Activity?.Title} — you're now registered.",
                        NotificationType.General);
                }
            }
        }

        registration.Status = ActivityRegistrationStatus.Cancelled;
        return Ok(ActivityRegistrationDto.FromEntity(registration));
    }

    [HttpPut("{id:int}/check-in")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<ActivityRegistrationDto>> SetCheckIn(int id, ActivityCheckInDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId) return Forbid();
        var registration = await registrations.GetByIdAsync(id);
        if (registration is null || await activities.GetByIdAsync(registration.ActivityId, schoolId) is not { } activity) return NotFound();
        if (!CanManage(activity)) return Forbid();
        if (registration.Status != ActivityRegistrationStatus.Registered)
            return BadRequest(new { message = "Only registered participants can be checked in." });

        await registrations.SetCheckInAsync(id, dto.CheckedIn, userId, schoolId);
        return Ok(ActivityRegistrationDto.FromEntity((await registrations.GetByIdAsync(id))!));
    }

    [HttpPut("{id:int}/payment")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<ActivityRegistrationDto>> MarkPaid(int id, ActivityPaymentDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var registration = await registrations.GetByIdAsync(id);
        if (registration is null || await activities.GetByIdAsync(registration.ActivityId, schoolId) is not { } activity) return NotFound();
        if (!CanManage(activity)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.PaymentReference)) return BadRequest(new { message = "Payment reference is required." });
        await registrations.SetPaidAsync(id, dto.PaymentReference.Trim(), schoolId);
        return Ok(ActivityRegistrationDto.FromEntity((await registrations.GetByIdAsync(id))!));
    }

    [HttpPut("{id:int}/consent-document")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ActivityRegistrationDto>> UploadConsentDocument(int id, IFormFile file)
    {
        if (currentTenant.SchoolId is not { } schoolId || fileStorage is null || storageOptions is null) return Forbid();
        var registration = await registrations.GetByIdAsync(id);
        if (registration is null || await activities.GetByIdAsync(registration.ActivityId, schoolId) is not { } activity) return NotFound();
        if (!currentUser.CanAccessStudent(registration.StudentId) && !CanManage(activity)) return Forbid();
        if (!activity.ConsentRequired) return Conflict(new { message = "This activity does not require consent." });
        if (file is not { Length: > 0 }) return BadRequest(new { message = "Select a consent document." });
        if (file.Length > storageOptions.Value.MaxFileSizeBytes) return BadRequest(new { message = "Consent document is too large." });
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFileTypes.IsExtensionAllowed(extension)) return BadRequest(new { message = "This file type is not supported." });
        await using (var check = file.OpenReadStream()) if (!await AllowedFileTypes.MatchesSignatureAsync(extension, check)) return BadRequest(new { message = "This file type is not supported." });
        var storedFileName = $"consent-{Guid.NewGuid()}{extension}";
        await using (var content = file.OpenReadStream()) await fileStorage.SaveFileAsync(storedFileName, content);
        if (registration.ConsentDocumentStoredFileName is not null) await fileStorage.DeleteFileAsync(registration.ConsentDocumentStoredFileName);
        await registrations.SetConsentDocumentAsync(id, file.FileName, storedFileName, file.ContentType, file.Length, schoolId);
        return Ok(ActivityRegistrationDto.FromEntity((await registrations.GetByIdAsync(id))!));
    }

    [HttpGet("{id:int}/consent-document")]
    public async Task<IActionResult> DownloadConsentDocument(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId || fileStorage is null) return Forbid();
        var registration = await registrations.GetByIdAsync(id);
        if (registration is null || await activities.GetByIdAsync(registration.ActivityId, schoolId) is not { } activity) return NotFound();
        if (!currentUser.CanAccessStudent(registration.StudentId) && !CanManage(activity)) return Forbid();
        if (registration.ConsentDocumentStoredFileName is null) return NotFound();
        var stream = await fileStorage.GetFileAsync(registration.ConsentDocumentStoredFileName);
        return stream is null ? NotFound() : File(stream, registration.ConsentDocumentContentType ?? "application/octet-stream", registration.ConsentDocumentFileName ?? "consent-document");
    }

    [HttpPost("activity/{activityId:int}/notify")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<IActionResult> NotifyParticipants(int activityId, ActivityParticipantMessageDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var activity = await activities.GetByIdAsync(activityId, schoolId);
        if (activity is null) return NotFound();
        if (!CanManage(activity)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message)) return BadRequest(new { message = "Title and message are required." });
        var selected = (await registrations.GetByActivityAsync(activityId)).Where(item => string.Equals(item.Status.ToString(), dto.RegistrationStatus, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var registration in selected)
        {
            var userIds = await recipients.ForStudentAsync(registration.StudentId);
            await notifications.NotifyManyTrackedAsync(userIds, dto.Title.Trim(), dto.Message.Trim(), NotificationType.General, $"/students/{registration.StudentId}/activities/{activityId}", dto.Priority, null);
        }
        return Ok(new { message = "Participant message queued.", count = selected.Count });
    }

    [HttpGet("activity/{activityId:int}/export")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<IActionResult> ExportActivityRegistrations(int activityId)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var activity = await activities.GetByIdAsync(activityId, schoolId);
        if (activity is null) return NotFound();
        if (!CanManage(activity)) return Forbid();

        var csv = new StringBuilder("Student,Registered at,Status,Consent confirmed,Checked in at\r\n");
        foreach (var item in await registrations.GetByActivityAsync(activityId))
        {
            csv.Append(Csv(item.Student is null ? string.Empty : $"{item.Student.FirstName} {item.Student.LastName}"));
            csv.Append(',').Append(item.RegistrationDate.ToString("O"));
            csv.Append(',').Append(item.Status);
            csv.Append(',').Append(item.ConsentConfirmed ? "Yes" : "No");
            csv.Append(',').Append(item.CheckedInAtUtc?.ToString("O") ?? string.Empty).Append("\r\n");
        }
        var safeTitle = string.Join('-', activity.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{safeTitle}-participants.csv");
    }

    private bool CanManage(Activity activity) => currentUser.Role is "Admin" or "Coordinator" || activity.OwnerUserId == currentUser.UserId;

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
