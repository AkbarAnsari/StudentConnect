using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssessmentsController(AssessmentStore assessments, ReferenceDataStore referenceData, ILogger<AssessmentsController> logger) : ControllerBase
{
    // GET /api/assessments
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetAssessments()
    {
        var all = await assessments.GetAllAsync();
        return Ok(all.Select(AssessmentDto.FromEntity));
    }

    // GET /api/assessments/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssessmentDto>> GetAssessmentById(int id)
    {
        var assessment = await assessments.GetByIdAsync(id);

        if (assessment is null)
        {
            return NotFound();
        }

        return Ok(AssessmentDto.FromEntity(assessment));
    }

    // GET /api/assessments/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetAssessmentsBySubject(int subjectId)
    {
        if (!await referenceData.SubjectExistsAsync(subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var bySubject = await assessments.GetBySubjectAsync(subjectId);
        return Ok(bySubject.Select(AssessmentDto.FromEntity));
    }

    // POST /api/assessments
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<AssessmentDto>> CreateAssessment(AssessmentCreateDto dto)
    {
        var validation = await ValidateAsync(dto.Title, dto.SubjectId, dto.MaximumMarks, dto.AssessmentType);
        if (validation is not null)
        {
            return validation;
        }

        Enum.TryParse<AssessmentType>(dto.AssessmentType, ignoreCase: true, out var type);

        var assessment = new Assessment
        {
            Title = dto.Title,
            SubjectId = dto.SubjectId,
            Date = dto.Date,
            MaximumMarks = dto.MaximumMarks,
            AssessmentType = type,
            Description = dto.Description,
        };

        int newId;
        try
        {
            newId = await assessments.CreateAsync(assessment);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save assessment {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the assessment. Please try again." });
        }

        var created = await assessments.GetByIdAsync(newId);
        var result = AssessmentDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetAssessmentById), new { id = newId }, result);
    }

    // PUT /api/assessments/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<AssessmentDto>> UpdateAssessment(int id, AssessmentUpdateDto dto)
    {
        var validation = await ValidateAsync(dto.Title, dto.SubjectId, dto.MaximumMarks, dto.AssessmentType);
        if (validation is not null)
        {
            return validation;
        }

        if (!await assessments.ExistsAsync(id))
        {
            return NotFound();
        }

        Enum.TryParse<AssessmentType>(dto.AssessmentType, ignoreCase: true, out var type);

        var assessment = new Assessment
        {
            Id = id,
            Title = dto.Title,
            SubjectId = dto.SubjectId,
            Date = dto.Date,
            MaximumMarks = dto.MaximumMarks,
            AssessmentType = type,
            Description = dto.Description,
        };

        try
        {
            await assessments.UpdateAsync(assessment);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update assessment {Id}", id);
            return StatusCode(500, new { message = "Unable to update the assessment. Please try again." });
        }

        var updated = await assessments.GetByIdAsync(id);
        return Ok(AssessmentDto.FromEntity(updated!));
    }

    // DELETE /api/assessments/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteAssessment(int id)
    {
        if (!await assessments.ExistsAsync(id))
        {
            return NotFound();
        }

        try
        {
            await assessments.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete assessment {Id}: {Message}", id, ex.Message);
            return Conflict(new { message = "Cannot delete an assessment that already has marks recorded against it." });
        }

        return NoContent();
    }

    private async Task<ActionResult?> ValidateAsync(string title, int subjectId, int maximumMarks, string assessmentType)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (maximumMarks <= 0)
        {
            return BadRequest(new { message = "MaximumMarks must be greater than zero." });
        }

        if (!Enum.TryParse<AssessmentType>(assessmentType, ignoreCase: true, out _))
        {
            return BadRequest(new { message = $"AssessmentType '{assessmentType}' is not valid." });
        }

        if (!await referenceData.SubjectExistsAsync(subjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        return null;
    }
}
