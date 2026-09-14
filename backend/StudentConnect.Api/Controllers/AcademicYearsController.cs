using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/academic-years")]
public class AcademicYearsController(
    AcademicYearStore academicYears,
    StudentStore students,
    SectionStore sections,
    ReferenceDataStore referenceData,
    IAuditService audit,
    Database db,
    ICurrentTenantService currentTenant,
    ILogger<AcademicYearsController> logger) : ControllerBase
{
    // GET /api/academic-years?includeArchived=true
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AcademicYearDto>>> GetAcademicYears(bool includeArchived = false)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await academicYears.GetAllAsync(schoolId, includeArchived);
        return Ok(all.Select(AcademicYearDto.FromEntity));
    }

    // POST /api/academic-years
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AcademicYearDto>> CreateAcademicYear(AcademicYearCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (dto.EndDate <= dto.StartDate)
        {
            return BadRequest(new { message = "EndDate must be after StartDate." });
        }

        int newId;
        try
        {
            newId = await academicYears.CreateAsync(new AcademicYear
            {
                Name = dto.Name,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsCurrent = dto.IsCurrent,
                SchoolId = schoolId,
            });
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to save academic year {Name}", dto.Name);
            return StatusCode(500, new { message = "Unable to save the academic year. Please try again." });
        }

        var created = await academicYears.GetByIdAsync(newId, schoolId);
        return CreatedAtAction(nameof(GetAcademicYears), null, AcademicYearDto.FromEntity(created!));
    }

    // DELETE /api/academic-years/{id} — archives (soft-deletes) rather than physically deleting,
    // so historical Terms/rollover records referencing this year stay intact.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAcademicYear(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await academicYears.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await academicYears.ArchiveAsync(id);
        await audit.LogAsync("Archive", "AcademicYear", id.ToString());
        return NoContent();
    }

    // POST /api/academic-years/{id}/restore
    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RestoreAcademicYear(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await academicYears.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await academicYears.RestoreAsync(id);
        await audit.LogAsync("Restore", "AcademicYear", id.ToString());
        return NoContent();
    }

    // POST /api/academic-years/{id}/rollover-preview — read-only; shows the proposed
    // Grade/Section promotion for every student in the school before anything is committed.
    [HttpPost("{id:int}/rollover-preview")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<RolloverStudentDto>>> PreviewRollover(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await academicYears.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        return Ok(await BuildRolloverPreviewAsync(schoolId));
    }

    // POST /api/academic-years/{id}/rollover-commit?force=true — promotes every non-excluded,
    // non-skipped student in one transaction. Blocked from re-running unless force=true, so a
    // second accidental click can't double-promote a cohort.
    [HttpPost("{id:int}/rollover-commit")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CommitRollover(int id, [FromQuery] bool force, [FromBody] RolloverCommitRequestDto request)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var academicYear = await academicYears.GetByIdAsync(id, schoolId);
        if (academicYear is null)
        {
            return NotFound();
        }

        if (academicYear.RolledOverAtUtc is not null && !force)
        {
            return Conflict(new { message = "This academic year has already been rolled over. Pass force=true to re-run it." });
        }

        var preview = await BuildRolloverPreviewAsync(schoolId);
        var overridesByStudentId = request.Overrides.ToDictionary(o => o.StudentId);
        var applied = new List<(int StudentId, string FromGradeName, int GradeId, int? SectionId)>();
        var skipped = 0;

        await db.WithTransactionAsync(async (connection, tx) =>
        {
            foreach (var row in preview)
            {
                overridesByStudentId.TryGetValue(row.StudentId, out var overrideRow);
                var gradeId = overrideRow?.GradeId ?? row.ProposedGradeId;

                if (row.Excluded || overrideRow?.Skip == true || gradeId is null)
                {
                    skipped++;
                    continue;
                }

                var sectionId = overrideRow?.SectionId ?? row.ProposedSectionId;
                await students.UpdateGradeSectionInTxAsync(connection, tx, row.StudentId, gradeId.Value, sectionId);
                applied.Add((row.StudentId, row.CurrentGradeName, gradeId.Value, sectionId));
            }

            await academicYears.MarkRolledOverInTxAsync(connection, tx, id);
            return true;
        });

        foreach (var (studentId, fromGradeName, gradeId, sectionId) in applied)
        {
            await audit.LogAsync(
                "Promote",
                "Student",
                studentId.ToString(),
                $"From {fromGradeName} to grade #{gradeId}" + (sectionId is null ? string.Empty : $", section #{sectionId}"));
        }

        logger.LogInformation("Academic year {AcademicYearId} rolled over: {Promoted} promoted, {Skipped} skipped", id, applied.Count, skipped);

        return Ok(new { promoted = applied.Count, skipped });
    }

    // Default mapping: next Grade in SequenceOrder (falling back to Id order), same-named
    // Section if one exists there. Non-Active students and students in the school's highest
    // grade are excluded (an admin decides those manually via the student status endpoint).
    private async Task<List<RolloverStudentDto>> BuildRolloverPreviewAsync(int schoolId)
    {
        var grades = (await referenceData.GetAllGradesAsync(schoolId))
            .OrderBy(g => g.SequenceOrder ?? g.Id)
            .ToList();
        var allStudents = await students.GetAllAsync(schoolId);
        var result = new List<RolloverStudentDto>();

        foreach (var student in allStudents)
        {
            var row = new RolloverStudentDto
            {
                StudentId = student.Id,
                StudentName = $"{student.FirstName} {student.LastName}",
                CurrentGradeId = student.GradeId,
                CurrentGradeName = student.Grade?.Name ?? string.Empty,
                CurrentSectionId = student.SectionId,
                CurrentSectionName = student.Section?.Name,
            };

            if (student.Status != StudentStatus.Active)
            {
                row.Excluded = true;
                row.ExclusionReason = $"Status is {student.Status}.";
                result.Add(row);
                continue;
            }

            var currentIndex = grades.FindIndex(g => g.Id == student.GradeId);
            var nextGrade = currentIndex >= 0 && currentIndex + 1 < grades.Count ? grades[currentIndex + 1] : null;

            if (nextGrade is null)
            {
                row.Excluded = true;
                row.ExclusionReason = "No next grade configured — this is the school's highest grade.";
                result.Add(row);
                continue;
            }

            row.ProposedGradeId = nextGrade.Id;
            row.ProposedGradeName = nextGrade.Name;

            if (student.SectionId is not null)
            {
                var destinationSections = await sections.GetByGradeAsync(nextGrade.Id);
                var matchingSection = destinationSections.FirstOrDefault(s => s.Name == student.Section!.Name);
                row.ProposedSectionId = matchingSection?.Id;
                row.ProposedSectionName = matchingSection?.Name;
            }

            result.Add(row);
        }

        return result;
    }
}
