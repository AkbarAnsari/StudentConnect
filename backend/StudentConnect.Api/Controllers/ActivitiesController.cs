using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController(StudentConnectDbContext db, ILogger<ActivitiesController> logger) : ControllerBase
{
    // GET /api/activities
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivities()
    {
        var activities = await db.Activities.Include(a => a.Grade).AsNoTracking().OrderBy(a => a.Date).ToListAsync();
        var result = new List<ActivityDto>();

        foreach (var activity in activities)
        {
            var currentRegistrations = await CountActiveRegistrations(activity.Id);
            result.Add(ActivityDto.FromEntity(activity, currentRegistrations));
        }

        return Ok(result);
    }

    // GET /api/activities/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityDto>> GetActivityById(int id)
    {
        var activity = await db.Activities.Include(a => a.Grade).AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null)
        {
            return NotFound();
        }

        var currentRegistrations = await CountActiveRegistrations(id);
        return Ok(ActivityDto.FromEntity(activity, currentRegistrations));
    }

    // GET /api/activities/grade/{gradeId}
    [HttpGet("grade/{gradeId:int}")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivitiesByGrade(int gradeId)
    {
        if (!await db.Grades.AnyAsync(g => g.Id == gradeId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var activities = await db.Activities
            .Include(a => a.Grade)
            .AsNoTracking()
            .Where(a => a.GradeId == gradeId)
            .OrderBy(a => a.Date)
            .ToListAsync();

        var result = new List<ActivityDto>();
        foreach (var activity in activities)
        {
            var currentRegistrations = await CountActiveRegistrations(activity.Id);
            result.Add(ActivityDto.FromEntity(activity, currentRegistrations));
        }

        return Ok(result);
    }

    // POST /api/activities
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<ActivityDto>> CreateActivity(ActivityCreateDto dto)
    {
        var validation = await ValidateAsync(dto.Title, dto.Location, dto.GradeId, dto.Capacity);
        if (validation is not null)
        {
            return validation;
        }

        var activity = new Activity
        {
            Title = dto.Title,
            Description = dto.Description,
            Date = dto.Date,
            Location = dto.Location,
            GradeId = dto.GradeId,
            Capacity = dto.Capacity,
            CreatedDate = DateTime.UtcNow,
        };

        db.Activities.Add(activity);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save activity {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the activity. Please try again." });
        }

        await db.Entry(activity).Reference(a => a.Grade).LoadAsync();
        return CreatedAtAction(nameof(GetActivityById), new { id = activity.Id }, ActivityDto.FromEntity(activity, 0));
    }

    // PUT /api/activities/{id} — Grade stays fixed after creation; only these fields can be corrected.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<ActivityDto>> UpdateActivity(int id, ActivityUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Location))
        {
            return BadRequest(new { message = "Title and Location are required." });
        }

        if (dto.Capacity <= 0)
        {
            return BadRequest(new { message = "Capacity must be greater than zero." });
        }

        var activity = await db.Activities.Include(a => a.Grade).FirstOrDefaultAsync(a => a.Id == id);
        if (activity is null)
        {
            return NotFound();
        }

        var currentRegistrations = await CountActiveRegistrations(id);
        if (dto.Capacity < currentRegistrations)
        {
            return BadRequest(new { message = $"Capacity cannot be less than the current number of active registrations ({currentRegistrations})." });
        }

        activity.Title = dto.Title;
        activity.Description = dto.Description;
        activity.Date = dto.Date;
        activity.Location = dto.Location;
        activity.Capacity = dto.Capacity;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update activity {Id}", id);
            return StatusCode(500, new { message = "Unable to update the activity. Please try again." });
        }

        return Ok(ActivityDto.FromEntity(activity, currentRegistrations));
    }

    // DELETE /api/activities/{id} — rejected if any registrations (even cancelled) exist, so
    // registration history is never silently orphaned by deleting the activity it refers to.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity is null)
        {
            return NotFound();
        }

        if (await db.ActivityRegistrations.AnyAsync(r => r.ActivityId == id))
        {
            return Conflict(new { message = "Cannot delete an activity that already has registrations." });
        }

        db.Activities.Remove(activity);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete activity {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the activity. Please try again." });
        }

        return NoContent();
    }

    private async Task<int> CountActiveRegistrations(int activityId) =>
        await db.ActivityRegistrations.CountAsync(r => r.ActivityId == activityId && r.Status == ActivityRegistrationStatus.Registered);

    private async Task<ActionResult?> ValidateAsync(string title, string location, int gradeId, int capacity)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location))
        {
            return BadRequest(new { message = "Title and Location are required." });
        }

        if (capacity <= 0)
        {
            return BadRequest(new { message = "Capacity must be greater than zero." });
        }

        if (!await db.Grades.AnyAsync(g => g.Id == gradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        return null;
    }
}
