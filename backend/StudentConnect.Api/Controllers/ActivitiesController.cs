using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController(ActivityStore activities, ActivityRegistrationStore registrations, ReferenceDataStore referenceData, ILogger<ActivitiesController> logger) : ControllerBase
{
    // GET /api/activities
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivities()
    {
        var all = await activities.GetAllAsync();
        var result = new List<ActivityDto>();

        foreach (var activity in all)
        {
            var currentRegistrations = await registrations.CountActiveAsync(activity.Id);
            var currentWaitlisted = await registrations.CountWaitlistedAsync(activity.Id);
            result.Add(ActivityDto.FromEntity(activity, currentRegistrations, currentWaitlisted));
        }

        return Ok(result);
    }

    // GET /api/activities/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityDto>> GetActivityById(int id)
    {
        var activity = await activities.GetByIdAsync(id);
        if (activity is null)
        {
            return NotFound();
        }

        var currentRegistrations = await registrations.CountActiveAsync(id);
        var currentWaitlisted = await registrations.CountWaitlistedAsync(id);
        return Ok(ActivityDto.FromEntity(activity, currentRegistrations, currentWaitlisted));
    }

    // GET /api/activities/grade/{gradeId}
    [HttpGet("grade/{gradeId:int}")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivitiesByGrade(int gradeId)
    {
        if (!await referenceData.GradeExistsAsync(gradeId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var byGrade = await activities.GetByGradeAsync(gradeId);
        var result = new List<ActivityDto>();
        foreach (var activity in byGrade)
        {
            var currentRegistrations = await registrations.CountActiveAsync(activity.Id);
            var currentWaitlisted = await registrations.CountWaitlistedAsync(activity.Id);
            result.Add(ActivityDto.FromEntity(activity, currentRegistrations, currentWaitlisted));
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

        int newId;
        try
        {
            newId = await activities.CreateAsync(activity);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save activity {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the activity. Please try again." });
        }

        var created = await activities.GetByIdAsync(newId);
        return CreatedAtAction(nameof(GetActivityById), new { id = newId }, ActivityDto.FromEntity(created!, 0));
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

        var activity = await activities.GetByIdAsync(id);
        if (activity is null)
        {
            return NotFound();
        }

        var currentRegistrations = await registrations.CountActiveAsync(id);
        if (dto.Capacity < currentRegistrations)
        {
            return BadRequest(new { message = $"Capacity cannot be less than the current number of active registrations ({currentRegistrations})." });
        }

        try
        {
            await activities.UpdateAsync(id, dto.Title, dto.Description, dto.Date, dto.Location, dto.Capacity);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update activity {Id}", id);
            return StatusCode(500, new { message = "Unable to update the activity. Please try again." });
        }

        activity.Title = dto.Title;
        activity.Description = dto.Description;
        activity.Date = dto.Date;
        activity.Location = dto.Location;
        activity.Capacity = dto.Capacity;
        return Ok(ActivityDto.FromEntity(activity, currentRegistrations));
    }

    // DELETE /api/activities/{id} — rejected if any registrations (even cancelled) exist, so
    // registration history is never silently orphaned by deleting the activity it refers to.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        if (await activities.GetByIdAsync(id) is null)
        {
            return NotFound();
        }

        if (await registrations.ExistsAnyForActivityAsync(id))
        {
            return Conflict(new { message = "Cannot delete an activity that already has registrations." });
        }

        try
        {
            await activities.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete activity {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the activity. Please try again." });
        }

        return NoContent();
    }

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

        if (!await referenceData.GradeExistsAsync(gradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        return null;
    }
}
