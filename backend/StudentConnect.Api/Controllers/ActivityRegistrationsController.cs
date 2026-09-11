using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/activity-registrations")]
public class ActivityRegistrationsController(StudentConnectDbContext db, ICurrentUserService currentUser, ILogger<ActivityRegistrationsController> logger) : ControllerBase
{
    // GET /api/activity-registrations/student/{studentId}
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<ActivityRegistrationDto>>> GetStudentRegistrations(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var registrations = await db.ActivityRegistrations
            .Include(r => r.Student).Include(r => r.Activity)
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.RegistrationDate)
            .Select(r => ActivityRegistrationDto.FromEntity(r))
            .ToListAsync();

        return Ok(registrations);
    }

    // GET /api/activity-registrations/activity/{activityId}
    [HttpGet("activity/{activityId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<ActivityRegistrationDto>>> GetActivityRegistrations(int activityId)
    {
        if (!await db.Activities.AnyAsync(a => a.Id == activityId))
        {
            return NotFound(new { message = "Activity not found." });
        }

        var registrations = await db.ActivityRegistrations
            .Include(r => r.Student).Include(r => r.Activity)
            .AsNoTracking()
            .Where(r => r.ActivityId == activityId)
            .OrderBy(r => r.RegistrationDate)
            .Select(r => ActivityRegistrationDto.FromEntity(r))
            .ToListAsync();

        return Ok(registrations);
    }

    // GET /api/activity-registrations/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityRegistrationDto>> GetRegistrationById(int id)
    {
        var registration = await db.ActivityRegistrations
            .Include(r => r.Student).Include(r => r.Activity)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

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

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null)
        {
            return BadRequest(new { message = "Student does not exist." });
        }

        var activity = await db.Activities.FirstOrDefaultAsync(a => a.Id == dto.ActivityId);
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
        await using var transaction = await db.Database.BeginTransactionAsync();

        var existing = await db.ActivityRegistrations
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.ActivityId == dto.ActivityId);

        if (existing is not null && existing.Status == ActivityRegistrationStatus.Registered)
        {
            return Conflict(new { message = "Student is already registered for this activity." });
        }

        var currentRegistrations = await db.ActivityRegistrations
            .CountAsync(r => r.ActivityId == dto.ActivityId && r.Status == ActivityRegistrationStatus.Registered);

        if (currentRegistrations >= activity.Capacity)
        {
            return Conflict(new { message = "This activity is already full." });
        }

        var registrationDate = DateTime.UtcNow;

        if (existing is not null)
        {
            // Re-registering after cancellation reactivates the same row instead of inserting a duplicate.
            existing.Status = ActivityRegistrationStatus.Registered;
            existing.RegistrationDate = registrationDate;
        }
        else
        {
            existing = new ActivityRegistration
            {
                StudentId = studentId,
                ActivityId = dto.ActivityId,
                RegistrationDate = registrationDate,
                Status = ActivityRegistrationStatus.Registered,
            };
            db.ActivityRegistrations.Add(existing);
        }

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to save registration for student {StudentId} on activity {ActivityId}", studentId, dto.ActivityId);
            return StatusCode(500, new { message = "Unable to register for this activity. Please try again." });
        }

        existing.Student = student;
        existing.Activity = activity;
        var result = ActivityRegistrationDto.FromEntity(existing);
        return CreatedAtAction(nameof(GetRegistrationById), new { id = existing.Id }, result);
    }

    // PUT /api/activity-registrations/{id}/cancel — Status flips to Cancelled; the row is kept as history.
    // The owning student (or a Teacher/Admin) may cancel a registration.
    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<ActivityRegistrationDto>> CancelRegistration(int id)
    {
        var registration = await db.ActivityRegistrations
            .Include(r => r.Student).Include(r => r.Activity)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (registration is null)
        {
            return NotFound();
        }

        if (!currentUser.CanAccessStudent(registration.StudentId))
        {
            return Forbid();
        }

        registration.Status = ActivityRegistrationStatus.Cancelled;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to cancel registration {Id}", id);
            return StatusCode(500, new { message = "Unable to cancel the registration. Please try again." });
        }

        return Ok(ActivityRegistrationDto.FromEntity(registration));
    }
}
