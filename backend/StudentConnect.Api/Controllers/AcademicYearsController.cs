using System.Text;
using System.Text.Json;
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
    RolloverRunStore rolloverRuns,
    ICurrentUserService currentUser,
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

    [HttpGet("rollover-runs")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<RolloverRunDto>>> GetRolloverRuns()
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        return Ok(await rolloverRuns.GetAllAsync(schoolId));
    }

    [HttpGet("rollover-runs/{runId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RolloverRunDetailDto>> GetRolloverRun(int runId)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var run = await rolloverRuns.GetByIdAsync(runId, schoolId);
        var snapshot = await rolloverRuns.GetSnapshotAsync(runId, schoolId);
        if (run is null || snapshot is null) return NotFound();
        return Ok(ToDetail(run, JsonSerializer.Deserialize<List<RolloverRunStudentDto>>(snapshot) ?? []));
    }

    [HttpGet("rollover-runs/{runId:int}/csv")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DownloadRolloverRun(int runId)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var run = await rolloverRuns.GetByIdAsync(runId, schoolId);
        var snapshot = await rolloverRuns.GetSnapshotAsync(runId, schoolId);
        if (run is null || snapshot is null) return NotFound();

        var rows = JsonSerializer.Deserialize<List<RolloverRunStudentDto>>(snapshot) ?? [];
        var csv = new StringBuilder("Student,Previous grade,Previous section,Destination grade,Destination section,Outcome,Reason\r\n");
        foreach (var row in rows)
        {
            csv.Append(Csv(row.StudentName)).Append(',').Append(Csv(row.CurrentGradeName)).Append(',')
                .Append(Csv(row.CurrentSectionName ?? string.Empty)).Append(',').Append(Csv(row.DestinationGradeName ?? string.Empty)).Append(',')
                .Append(Csv(row.DestinationSectionName ?? string.Empty)).Append(',').Append(Csv(row.Outcome)).Append(',')
                .Append(Csv(row.Reason ?? string.Empty)).Append("\r\n");
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"rollover-{run.AcademicYearName}-{run.Id}.csv");
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
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } actorUserId)
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

        var duplicateOverride = request.Overrides.GroupBy(o => o.StudentId).FirstOrDefault(group => group.Count() > 1);
        if (duplicateOverride is not null)
            return BadRequest(new { message = $"Student #{duplicateOverride.Key} appears more than once in the rollover overrides." });

        var preview = await BuildRolloverPreviewAsync(schoolId);
        var previewByStudentId = preview.ToDictionary(row => row.StudentId);
        var overridesByStudentId = request.Overrides.ToDictionary(row => row.StudentId);
        var unknownOverride = request.Overrides.FirstOrDefault(row => !previewByStudentId.ContainsKey(row.StudentId));
        if (unknownOverride is not null)
            return BadRequest(new { message = $"Student #{unknownOverride.StudentId} does not belong to this school." });

        var gradesById = (await referenceData.GetAllGradesAsync(schoolId)).ToDictionary(grade => grade.Id);
        var sectionsById = (await sections.GetAllAsync(schoolId)).ToDictionary(section => section.Id);
        foreach (var overrideRow in request.Overrides.Where(row => !row.Skip))
        {
            var previewRow = previewByStudentId[overrideRow.StudentId];
            var gradeId = overrideRow.GradeId ?? previewRow.ProposedGradeId;
            if (gradeId is null || !gradesById.ContainsKey(gradeId.Value))
                return BadRequest(new { message = $"Select a valid destination grade for {previewRow.StudentName}." });

            var sectionId = overrideRow.GradeId is not null ? overrideRow.SectionId : overrideRow.SectionId ?? previewRow.ProposedSectionId;
            if (sectionId is not null && (!sectionsById.TryGetValue(sectionId.Value, out var section) || section.GradeId != gradeId))
                return BadRequest(new { message = $"The destination section for {previewRow.StudentName} does not belong to the selected grade." });
        }

        var applied = new List<(int StudentId, string FromGradeName, int GradeId, int? SectionId)>();
        var snapshotRows = new List<RolloverRunStudentDto>();

        foreach (var row in preview)
        {
            overridesByStudentId.TryGetValue(row.StudentId, out var overrideRow);
            var gradeId = overrideRow?.GradeId ?? row.ProposedGradeId;
            var sectionId = overrideRow?.GradeId is not null ? overrideRow.SectionId : overrideRow?.SectionId ?? row.ProposedSectionId;
            var skipReason = row.Excluded ? row.ExclusionReason : overrideRow?.Skip == true ? "Skipped by administrator." : gradeId is null ? "No destination grade selected." : null;

            if (skipReason is null)
                applied.Add((row.StudentId, row.CurrentGradeName, gradeId!.Value, sectionId));

            snapshotRows.Add(new RolloverRunStudentDto
            {
                StudentId = row.StudentId,
                StudentName = row.StudentName,
                CurrentGradeName = row.CurrentGradeName,
                CurrentSectionName = row.CurrentSectionName,
                DestinationGradeId = skipReason is null ? gradeId : null,
                DestinationGradeName = skipReason is null ? gradesById[gradeId!.Value].Name : null,
                DestinationSectionId = skipReason is null ? sectionId : null,
                DestinationSectionName = skipReason is null && sectionId is not null ? sectionsById[sectionId.Value].Name : null,
                Outcome = skipReason is null ? "Promoted" : "Skipped",
                Reason = skipReason,
            });
        }

        var skipped = snapshotRows.Count(row => row.Outcome == "Skipped");
        var runId = 0;

        await db.WithTransactionAsync(async (connection, tx) =>
        {
            foreach (var row in applied)
            {
                await students.UpdateGradeSectionInTxAsync(connection, tx, row.StudentId, row.GradeId, row.SectionId);
            }

            await academicYears.MarkRolledOverInTxAsync(connection, tx, id);
            runId = await RolloverRunStore.InsertInTxAsync(
                connection, tx, schoolId, id, actorUserId, force, applied.Count, skipped, JsonSerializer.Serialize(snapshotRows));
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

        return Ok(new { runId, promoted = applied.Count, skipped });
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

    private static RolloverRunDetailDto ToDetail(RolloverRunDto run, List<RolloverRunStudentDto> students) => new()
    {
        Id = run.Id,
        AcademicYearId = run.AcademicYearId,
        AcademicYearName = run.AcademicYearName,
        ActorUserId = run.ActorUserId,
        ActorName = run.ActorName,
        CreatedAtUtc = run.CreatedAtUtc,
        IsForced = run.IsForced,
        PromotedCount = run.PromotedCount,
        SkippedCount = run.SkippedCount,
        Students = students,
    };

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
