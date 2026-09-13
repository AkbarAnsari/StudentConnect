using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/activity-registrations")]
public class ActivityRegistrationsController(
    ActivityRegistrationStore registrations,
    StudentStore students,
    ActivityStore activities,
    Database db,
    ICurrentUserService currentUser,
    ILogger<ActivityRegistrationsController> logger) : ControllerBase
{
    // GET /api/activity-registrations/student/{studentId}
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<ActivityRegistrationDto>>> GetStudentRegistrations(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await students.ExistsAsync(studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var byStudent = await registrations.GetByStudentAsync(studentId);
        return Ok(byStudent.Select(ActivityRegistrationDto.FromEntity));
    }

    // GET /api/activity-registrations/activity/{activityId}
    [HttpGet("activity/{activityId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<ActivityRegistrationDto>>> GetActivityRegistrations(int activityId)
    {
        if (await activities.GetByIdAsync(activityId) is null)
        {
            return NotFound(new { message = "Activity not found." });
        }

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

        var student = await students.GetByIdAsync(studentId);
        if (student is null)
        {
            return BadRequest(new { message = "Student does not exist." });
        }

        var activity = await activities.GetByIdAsync(dto.ActivityId);
        if (activity is null)
        {
            return BadRequest(new { message = "Activity does not exist." });
        }

        if (activity.Date < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Registration is closed because the activity has already occurred." });
        }

        if (student.GradeId != activity.GradeId)
        {
            return BadRequest(new { message = "Student is not eligible for this activity." });
        }

        // A transaction narrows (without fully eliminating) the race where two near-simultaneous
        // requests both read a capacity count that's about to become stale — a stronger guarantee
        // (e.g. serializable isolation or a concurrency token) would be needed for a highly
        // concurrent production system; that's out of scope for this learning project.
        int registrationId;
        var registrationDate = DateTime.UtcNow;
        try
        {
            var (Id, IsFull, AlreadyRegistered) = await db.WithTransactionAsync(async (connection, tx) =>
            {
                var (existingId, existingStatus) = await registrations.GetExistingInTxAsync(connection, tx, studentId, dto.ActivityId);

                if (existingId is not null && existingStatus == ActivityRegistrationStatus.Registered)
                {
                    return (existingId.Value, false, true);
                }

                var currentRegistrations = await registrations.CountActiveInTxAsync(connection, tx, dto.ActivityId);
                if (currentRegistrations >= activity.Capacity)
                {
                    return (0, true, false);
                }

                if (existingId is not null)
                {
                    await registrations.ReactivateInTxAsync(connection, tx, existingId.Value, registrationDate);
                    return (existingId.Value, false, false);
                }

                var newId = await registrations.InsertInTxAsync(connection, tx, studentId, dto.ActivityId, registrationDate);
                return (newId, false, false);
            });

            if (AlreadyRegistered)
            {
                return Conflict(new { message = "Student is already registered for this activity." });
            }

            if (IsFull)
            {
                return Conflict(new { message = "This activity is already full." });
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
    // The owning student (or a Teacher/Admin) may cancel a registration.
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

        try
        {
            await registrations.UpdateStatusAsync(id, ActivityRegistrationStatus.Cancelled);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to cancel registration {Id}", id);
            return StatusCode(500, new { message = "Unable to cancel the registration. Please try again." });
        }

        registration.Status = ActivityRegistrationStatus.Cancelled;
        return Ok(ActivityRegistrationDto.FromEntity(registration));
    }
}
