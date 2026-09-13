using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Storage;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/learning-materials")]
public class LearningMaterialsController(
    LearningMaterialStore materials,
    StudentStore students,
    ReferenceDataStore referenceData,
    IFileStorage fileStorage,
    IOptions<FileStorageOptions> storageOptions,
    ICurrentUserService currentUser,
    ILogger<LearningMaterialsController> logger) : ControllerBase
{
    // GET /api/learning-materials?subjectId=&gradeId= — metadata only, never the file bytes.
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetLearningMaterials(int? subjectId, int? gradeId)
    {
        var all = await materials.GetAllAsync(subjectId, gradeId);
        return Ok(all.Select(LearningMaterialDto.FromEntity));
    }

    // GET /api/learning-materials/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LearningMaterialDto>> GetLearningMaterialById(int id)
    {
        var material = await materials.GetByIdAsync(id);

        if (material is null)
        {
            return NotFound();
        }

        return Ok(LearningMaterialDto.FromEntity(material));
    }

    // GET /api/learning-materials/grade/{gradeId}
    [HttpGet("grade/{gradeId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetByGrade(int gradeId)
    {
        if (!await referenceData.GradeExistsAsync(gradeId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var byGrade = await materials.GetByGradeAsync(gradeId);
        return Ok(byGrade.Select(LearningMaterialDto.FromEntity));
    }

    // GET /api/learning-materials/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetBySubject(int subjectId)
    {
        if (!await referenceData.SubjectExistsAsync(subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var bySubject = await materials.GetBySubjectAsync(subjectId);
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

        if (!await referenceData.TeacherExistsAsync(teacherId))
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var byTeacher = await materials.GetByTeacherAsync(teacherId);
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

        var student = await students.GetByIdAsync(studentId);
        if (student is null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var subjectIds = await students.GetSubjectIdsForStudentAsync(studentId);
        var forStudent = await materials.GetForStudentAsync(student.GradeId, subjectIds);
        return Ok(forStudent.Select(LearningMaterialDto.FromEntity));
    }

    // POST /api/learning-materials — multipart/form-data upload.
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<LearningMaterialDto>> UploadLearningMaterial([FromForm] LearningMaterialUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (!await referenceData.SubjectExistsAsync(request.SubjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await referenceData.GradeExistsAsync(request.GradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (!await referenceData.TeacherExistsAsync(request.TeacherId))
        {
            return BadRequest(new { message = "Teacher does not exist." });
        }

        if (!currentUser.CanActAsTeacher(request.TeacherId))
        {
            return Forbid();
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
            SubjectId = request.SubjectId,
            GradeId = request.GradeId,
            TeacherId = request.TeacherId,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedDate = DateTime.UtcNow,
        };

        int newId;
        try
        {
            newId = await materials.CreateAsync(material);
        }
        catch (PostgresException ex)
        {
            // The file was saved but its metadata couldn't be — clean up rather than leave an orphaned file.
            logger.LogError(ex, "Failed to save learning material metadata for {Title}; removing orphaned file", request.Title);
            await fileStorage.DeleteFileAsync(storedFileName);
            return StatusCode(500, new { message = "Unable to upload learning material." });
        }

        var created = await materials.GetByIdAsync(newId);
        var result = LearningMaterialDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetLearningMaterialById), new { id = newId }, result);
    }

    // GET /api/learning-materials/{id}/download
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadLearningMaterial(int id)
    {
        var material = await materials.GetByIdAsync(id);
        if (material is null)
        {
            return NotFound(new { message = "Learning material not found." });
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
        var storedFileName = await materials.GetStoredFileNameAsync(id);
        if (storedFileName is null)
        {
            return NotFound();
        }

        // Deleting a file that's already missing is not an error — the end state (no file) is what we want.
        await fileStorage.DeleteFileAsync(storedFileName);

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
}
