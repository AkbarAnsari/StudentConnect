using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/curriculum-defaults")]
[Authorize(Roles = "Admin")]
public class CurriculumDefaultsController(
    CurriculumDefaultStore defaults,
    ReferenceDataStore referenceData,
    SectionStore sections,
    ICurrentTenantService currentTenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CurriculumDefaultDto>> Get(int gradeId, int? sectionId)
    {
        var error = await ValidateScopeAsync(gradeId, sectionId);
        if (error is not null) return error;
        return Ok(new CurriculumDefaultDto { GradeId = gradeId, SectionId = sectionId, SubjectIds = await defaults.GetSubjectIdsAsync(gradeId, sectionId) });
    }

    [HttpGet("resolved")]
    public async Task<ActionResult<CurriculumDefaultDto>> GetResolved(int gradeId, int? sectionId)
    {
        var error = await ValidateScopeAsync(gradeId, sectionId);
        if (error is not null) return error;
        return Ok(new CurriculumDefaultDto { GradeId = gradeId, SectionId = sectionId, SubjectIds = await defaults.GetResolvedSubjectIdsAsync(gradeId, sectionId) });
    }

    [HttpPut]
    public async Task<ActionResult<CurriculumDefaultDto>> Replace(CurriculumDefaultReplaceDto dto)
    {
        var error = await ValidateScopeAsync(dto.GradeId, dto.SectionId);
        if (error is not null) return error;
        var schoolId = currentTenant.SchoolId!.Value;
        var subjectIds = dto.SubjectIds.Distinct().ToList();
        if ((await referenceData.GetSubjectsByIdsAsync(subjectIds, schoolId)).Count != subjectIds.Count)
            return BadRequest(new { message = "One or more subjects do not belong to this school." });
        await defaults.ReplaceAsync(dto.GradeId, dto.SectionId, subjectIds);
        return Ok(new CurriculumDefaultDto { GradeId = dto.GradeId, SectionId = dto.SectionId, SubjectIds = subjectIds });
    }

    private async Task<ActionResult?> ValidateScopeAsync(int gradeId, int? sectionId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }
        if (!await referenceData.GradeExistsAsync(gradeId, schoolId))
        {
            return NotFound(new { message = "Grade not found." });
        }
        if (sectionId is { } id && !await sections.BelongsToGradeAsync(id, gradeId))
        {
            return BadRequest(new { message = "Section does not belong to the selected grade." });
        }
        return null;
    }
}