using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using StudentConnect.Infrastructure.Storage;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/learning-materials")]
public class LearningMaterialsController(
    LearningMaterialStore materials,
    StudentStore students,
    ReferenceDataStore referenceData,
    TeacherSubjectAssignmentStore assignments,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> storageOptions,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    ILogger<LearningMaterialsController> logger,
    NotificationRecipientResolver? recipients = null,
    INotificationService? notifications = null) : ControllerBase
{
    // GET /api/learning-materials?subjectId=&gradeId= — metadata only, never the file bytes.
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetLearningMaterials(int? subjectId, int? gradeId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await materials.GetAllAsync(schoolId, subjectId, gradeId);
        if (currentUser.Role == "Teacher") all = all.Where(item => item.TeacherId == currentUser.TeacherId).ToList();
        return Ok(all.Select(LearningMaterialDto.FromEntity));
    }

    // GET /api/learning-materials/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LearningMaterialDto>> GetLearningMaterialById(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var material = await materials.GetByIdAsync(id, schoolId);

        if (material is null)
        {
            return NotFound();
        }

        if (!await CanReadMaterialAsync(material, schoolId))
        {
            return Forbid();
        }

        return Ok(LearningMaterialDto.FromEntity(material));
    }

    // GET /api/learning-materials/grade/{gradeId}
    [HttpGet("grade/{gradeId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetByGrade(int gradeId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.GradeExistsAsync(gradeId, schoolId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var byGrade = await materials.GetByGradeAsync(gradeId, schoolId);
        if (currentUser.Role == "Teacher") byGrade = byGrade.Where(item => item.TeacherId == currentUser.TeacherId).ToList();
        return Ok(byGrade.Select(LearningMaterialDto.FromEntity));
    }

    // GET /api/learning-materials/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetBySubject(int subjectId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.SubjectExistsAsync(subjectId, schoolId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var bySubject = await materials.GetBySubjectAsync(subjectId, schoolId);
        if (currentUser.Role == "Teacher") bySubject = bySubject.Where(item => item.TeacherId == currentUser.TeacherId).ToList();
        return Ok(bySubject.Select(LearningMaterialDto.FromEntity));
    }

    // GET /api/learning-materials/teacher/{teacherId}
    [HttpGet("teacher/{teacherId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetByTeacher(int teacherId)
    {
        if (!currentUser.CanActAsTeacher(teacherId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.TeacherExistsAsync(teacherId, schoolId))
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var byTeacher = await materials.GetByTeacherAsync(teacherId, schoolId);
        return Ok(byTeacher.Select(LearningMaterialDto.FromEntity));
    }

    // GET /api/learning-materials/student/{studentId} — relevant materials, derived from the
    // student's own grade and the subjects they study (reusing the Phase 5 StudentSubject relationship).
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetForStudent(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
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
            return NotFound(new { message = "Student not found." });
        }

        var subjectIds = await students.GetSubjectIdsForStudentAsync(studentId);
        var forStudent = await materials.GetForStudentAsync(student.GradeId, student.SectionId, subjectIds, schoolId);
        return Ok(forStudent.Select(LearningMaterialDto.FromEntity));
    }

    // POST /api/learning-materials — multipart/form-data upload.
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<LearningMaterialDto>> UploadLearningMaterial([FromForm] LearningMaterialUploadRequest request)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (!await referenceData.SubjectExistsAsync(request.SubjectId, schoolId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await referenceData.GradeExistsAsync(request.GradeId, schoolId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (!await referenceData.TeacherExistsAsync(request.TeacherId, schoolId))
        {
            return BadRequest(new { message = "Teacher does not exist." });
        }

        if (!await assignments.ExistsAsync(request.TeacherId, request.SubjectId, request.SectionId, schoolId))
        {
            return BadRequest(new { message = "The selected Teacher is not assigned to this subject and section." });
        }

        if (!currentUser.CanActAsTeacher(request.TeacherId))
        {
            return Forbid();
        }

        if (!TryResolveInitialStatus(request.Status, request.ScheduledForUtc, out var lifecycle, out var lifecycleError))
        {
            return BadRequest(new { message = lifecycleError });
        }

        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select a file to upload." });
        }

        if (file.Length > storageOptions.Value.MaxFileSizeBytes)
        {
            return BadRequest(new { message = $"File exceeds the maximum allowed size of {storageOptions.Value.MaxFileSizeBytes / (1024 * 1024)} MB." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFileTypes.IsExtensionAllowed(extension))
        {
            return BadRequest(new { message = "This file type is not supported." });
        }

        await using (var checkStream = file.OpenReadStream())
        {
            if (!await AllowedFileTypes.MatchesSignatureAsync(extension, checkStream))
            {
                return BadRequest(new { message = "This file type is not supported." });
            }
        }

        // Never trust the client-supplied file name for storage — generate our own safe, unique name.
        var storedFileName = $"{Guid.NewGuid()}{extension}";

        try
        {
            await using var uploadStream = file.OpenReadStream();
            await fileStorage.SaveFileAsync(storedFileName, uploadStream);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to save uploaded file for material {Title}", request.Title);
            return StatusCode(500, new { message = "Unable to upload learning material." });
        }

        var material = new LearningMaterial
        {
            Title = request.Title,
            Description = request.Description,
            Topic = (request.Topic ?? string.Empty).Trim(),
            SubjectId = request.SubjectId,
            GradeId = request.GradeId,
            SectionId = request.SectionId,
            TeacherId = request.TeacherId,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedDate = DateTime.UtcNow,
            Status = lifecycle,
            ScheduledForUtc = lifecycle == ContentLifecycleStatus.Scheduled ? request.ScheduledForUtc : null,
            PublishedAtUtc = lifecycle == ContentLifecycleStatus.Published ? DateTime.UtcNow : null,
        };

        int newId;
        try
        {
            newId = await materials.CreateAsync(material, currentUser.UserId);
        }
        catch (PostgresException ex)
        {
            // The file was saved but its metadata couldn't be — clean up rather than leave an orphaned file.
            logger.LogError(ex, "Failed to save learning material metadata for {Title}; removing orphaned file", request.Title);
            await fileStorage.DeleteFileAsync(storedFileName);
            return StatusCode(500, new { message = "Unable to upload learning material." });
        }

        var created = await materials.GetByIdAsync(newId, schoolId);
        if (lifecycle == ContentLifecycleStatus.Published)
        {
            await NotifyMaterialReleasedAsync(created!, schoolId);
        }
        var result = LearningMaterialDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetLearningMaterialById), new { id = newId }, result);
    }

    [HttpGet("{id:int}/versions")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialVersionDto>>> GetVersions(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var material = await materials.GetByIdAsync(id, schoolId);
        if (material is null) return NotFound();
        if (!currentUser.CanActAsTeacher(material.TeacherId)) return Forbid();
        return Ok(await materials.GetVersionsAsync(id, schoolId));
    }

    [HttpGet("{id:int}/versions/{versionId:long}/download")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DownloadVersion(int id, long versionId)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var material = await materials.GetByIdAsync(id, schoolId);
        if (material is null) return NotFound();
        if (!currentUser.CanActAsTeacher(material.TeacherId)) return Forbid();
        var version = await materials.GetVersionFileAsync(id, versionId, schoolId);
        if (version is null) return NotFound();
        var stream = await fileStorage.GetFileAsync(version.StoredFileName);
        return stream is null
            ? NotFound(new { message = "This version's file is unavailable." })
            : File(stream, version.ContentType, version.FileName);
    }

    [HttpPost("{id:int}/replace-file")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<LearningMaterialDto>> ReplaceFile(int id, [FromForm] LearningMaterialReplacementRequest request)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var material = await materials.GetByIdAsync(id, schoolId);
        if (material is null) return NotFound();
        if (!currentUser.CanActAsTeacher(material.TeacherId)) return Forbid();
        if (material.Status != ContentLifecycleStatus.Draft)
            return Conflict(new { message = "Return this material to Draft before replacing its file." });
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
            return BadRequest(new { message = "A replacement reason is required." });

        var file = request.File;
        if (file is not { Length: > 0 }) return BadRequest(new { message = "Please select a replacement file." });
        if (file.Length > storageOptions.Value.MaxFileSizeBytes)
            return BadRequest(new { message = $"File exceeds the maximum allowed size of {storageOptions.Value.MaxFileSizeBytes / (1024 * 1024)} MB." });
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFileTypes.IsExtensionAllowed(extension)) return BadRequest(new { message = "This file type is not supported." });
        await using (var checkStream = file.OpenReadStream())
        {
            if (!await AllowedFileTypes.MatchesSignatureAsync(extension, checkStream))
                return BadRequest(new { message = "This file type is not supported." });
        }

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        try
        {
            await using var uploadStream = file.OpenReadStream();
            await fileStorage.SaveFileAsync(storedFileName, uploadStream);
            await materials.ReplaceFileAsync(id, file.FileName, storedFileName, file.ContentType, file.Length, request.ChangeReason.Trim(), currentUser.UserId);
        }
        catch (Exception ex) when (ex is IOException or PostgresException)
        {
            logger.LogError(ex, "Failed to replace file for learning material {Id}", id);
            await fileStorage.DeleteFileAsync(storedFileName);
            return StatusCode(500, new { message = "Unable to replace the learning material file." });
        }

        return Ok(LearningMaterialDto.FromEntity((await materials.GetByIdAsync(id, schoolId))!));
    }

    // GET /api/learning-materials/{id}/download
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadLearningMaterial(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var material = await materials.GetByIdAsync(id, schoolId);
        if (material is null)
        {
            return NotFound(new { message = "Learning material not found." });
        }

        if (!await CanReadMaterialAsync(material, schoolId))
        {
            return Forbid();
        }

        var stream = await fileStorage.GetFileAsync(material.StoredFileName);
        if (stream is null)
        {
            return NotFound(new { message = "Learning material file is unavailable." });
        }

        return File(stream, material.ContentType, material.FileName);
    }

    // DELETE /api/learning-materials/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteLearningMaterial(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var material = await materials.GetByIdAsync(id, schoolId);
        if (material is null)
        {
            return NotFound();
        }

        if (!currentUser.CanActAsTeacher(material.TeacherId))
        {
            return Forbid();
        }

        if (material.Status != ContentLifecycleStatus.Draft)
        {
            return Conflict(new { message = "Only Draft materials can be permanently deleted. Archive released materials instead." });
        }

        var storedFileNames = await materials.GetStoredFileNamesAsync(id);
        foreach (var storedFileName in storedFileNames)
        {
            await fileStorage.DeleteFileAsync(storedFileName);
        }

        try
        {
            await materials.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete learning material {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the learning material. Please try again." });
        }

        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<LearningMaterialDto>> PublishMaterial(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Published);

    [HttpPost("{id:int}/schedule")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<LearningMaterialDto>> ScheduleMaterial(int id, ContentScheduleDto dto) =>
        dto.ScheduledForUtc <= DateTime.UtcNow
            ? Task.FromResult<ActionResult<LearningMaterialDto>>(BadRequest(new { message = "Scheduled time must be in the future." }))
            : ChangeLifecycleAsync(id, ContentLifecycleStatus.Scheduled, dto.ScheduledForUtc);

    [HttpPost("{id:int}/close")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<LearningMaterialDto>> CloseMaterial(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Closed);

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<LearningMaterialDto>> ArchiveMaterial(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Archived);

    [HttpPost("{id:int}/reopen")]
    [Authorize(Roles = "Teacher,Admin")]
    public Task<ActionResult<LearningMaterialDto>> ReopenMaterial(int id) => ChangeLifecycleAsync(id, ContentLifecycleStatus.Draft);

    private async Task<bool> CanReadMaterialAsync(LearningMaterial material, int schoolId)
    {
        if (currentUser.IsTeacherOrAdmin)
        {
            return true;
        }

        if (!IsLearnerVisible(material))
        {
            return false;
        }

        if (currentUser.StudentId is { } studentId)
        {
            return await StudentCanReadMaterialAsync(studentId, material, schoolId);
        }

        foreach (var childStudentId in currentUser.ChildStudentIds)
        {
            if (await StudentCanReadMaterialAsync(childStudentId, material, schoolId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> StudentCanReadMaterialAsync(int studentId, LearningMaterial material, int schoolId)
    {
        var student = await students.GetByIdAsync(studentId, schoolId);
        if (student?.GradeId != material.GradeId ||
            (material.SectionId is not null && student.SectionId != material.SectionId))
        {
            return false;
        }

        var subjectIds = await students.GetSubjectIdsForStudentAsync(studentId);
        return subjectIds.Contains(material.SubjectId);
    }

    private async Task<ActionResult<LearningMaterialDto>> ChangeLifecycleAsync(
        int id,
        ContentLifecycleStatus status,
        DateTime? scheduledForUtc = null)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var material = await materials.GetByIdAsync(id, schoolId);
        if (material is null)
        {
            return NotFound();
        }

        if (!currentUser.CanActAsTeacher(material.TeacherId))
        {
            return Forbid();
        }

        await materials.SetLifecycleAsync(id, status, scheduledForUtc);
        var updated = (await materials.GetByIdAsync(id, schoolId))!;
        if (status == ContentLifecycleStatus.Published && material.Status != ContentLifecycleStatus.Published)
        {
            await NotifyMaterialReleasedAsync(updated, schoolId);
        }
        return Ok(LearningMaterialDto.FromEntity(updated));
    }

    private async Task NotifyMaterialReleasedAsync(LearningMaterial material, int schoolId)
    {
        if (recipients is null || notifications is null) return;
        var studentRows = material.SectionId is { } sectionId
            ? await students.GetActiveBySectionAndSubjectAsync(sectionId, material.SubjectId, schoolId)
            : await students.GetActiveByGradeAndSubjectAsync(material.GradeId, material.SubjectId, schoolId);
        foreach (var student in studentRows)
        {
            var userIds = await recipients.ForStudentAsync(student.Id);
            await notifications.NotifyManyForLearningMaterialAsync(
                userIds,
                "New learning material",
                $"{material.Subject!.Name}: {material.Title}",
                NotificationType.LearningMaterialReleased,
                $"/students/{student.Id}/materials",
                material.Id);
        }
    }

    private static bool IsLearnerVisible(LearningMaterial material) =>
        material.Status == ContentLifecycleStatus.Published ||
        material.Status == ContentLifecycleStatus.Scheduled && material.ScheduledForUtc <= DateTime.UtcNow;

    private static bool TryResolveInitialStatus(
        string value,
        DateTime? scheduledForUtc,
        out ContentLifecycleStatus status,
        out string? error)
    {
        if (!Enum.TryParse(value, true, out status) || status is ContentLifecycleStatus.Closed or ContentLifecycleStatus.Archived)
        {
            error = "Status must be Draft, Scheduled, or Published.";
            return false;
        }

        if (status == ContentLifecycleStatus.Scheduled && (scheduledForUtc is null || scheduledForUtc <= DateTime.UtcNow))
        {
            error = "A future scheduled time is required.";
            return false;
        }

        error = null;
        return true;
    }
}
