using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssessmentsController(StudentConnectDbContext db, ILogger<AssessmentsController> logger) : ControllerBase
{
    // GET /api/assessments
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetAssessments()
    {
        var assessments = await db.Assessments
            .Include(a => a.Subject)
            .AsNoTracking()
            .OrderBy(a => a.SubjectId).ThenBy(a => a.Date)
            .Select(a => AssessmentDto.FromEntity(a))
            .ToListAsync();

        return Ok(assessments);
    }

    // GET /api/assessments/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssessmentDto>> GetAssessmentById(int id)
    {
        var assessment = await db.Assessments
            .Include(a => a.Subject)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

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
        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var assessments = await db.Assessments
            .Include(a => a.Subject)
            .AsNoTracking()
            .Where(a => a.SubjectId == subjectId)
            .OrderBy(a => a.Date)
            .Select(a => AssessmentDto.FromEntity(a))
            .ToListAsync();

        return Ok(assessments);
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

        db.Assessments.Add(assessment);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save assessment {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the assessment. Please try again." });
        }

        await db.Entry(assessment).Reference(a => a.Subject).LoadAsync();
        var result = AssessmentDto.FromEntity(assessment);
        return CreatedAtAction(nameof(GetAssessmentById), new { id = assessment.Id }, result);
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

        var assessment = await db.Assessments.Include(a => a.Subject).FirstOrDefaultAsync(a => a.Id == id);
        if (assessment is null)
        {
            return NotFound();
        }

        Enum.TryParse<AssessmentType>(dto.AssessmentType, ignoreCase: true, out var type);

        assessment.Title = dto.Title;
        assessment.SubjectId = dto.SubjectId;
        assessment.Date = dto.Date;
        assessment.MaximumMarks = dto.MaximumMarks;
        assessment.AssessmentType = type;
        assessment.Description = dto.Description;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update assessment {Id}", id);
            return StatusCode(500, new { message = "Unable to update the assessment. Please try again." });
        }

        await db.Entry(assessment).Reference(a => a.Subject).LoadAsync();
        return Ok(AssessmentDto.FromEntity(assessment));
    }

    // DELETE /api/assessments/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteAssessment(int id)
    {
        var assessment = await db.Assessments.FindAsync(id);
        if (assessment is null)
        {
            return NotFound();
        }

        db.Assessments.Remove(assessment);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete assessment {Id}", id);
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

        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        return null;
    }
}
