using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;
using StudentConnect.Api.Storage;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/learning-materials")]
public class LearningMaterialsController(
    StudentConnectDbContext db,
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
        var query = db.LearningMaterials
            .Include(m => m.Subject).Include(m => m.Grade).Include(m => m.Teacher)
            .AsNoTracking()
            .AsQueryable();

        if (subjectId is not null)
        {
            query = query.Where(m => m.SubjectId == subjectId);
        }

        if (gradeId is not null)
        {
            query = query.Where(m => m.GradeId == gradeId);
        }

        var materials = await query
            .OrderByDescending(m => m.UploadedDate)
            .Select(m => LearningMaterialDto.FromEntity(m))
            .ToListAsync();

        return Ok(materials);
    }

    // GET /api/learning-materials/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<LearningMaterialDto>> GetLearningMaterialById(int id)
    {
        var material = await db.LearningMaterials
            .Include(m => m.Subject).Include(m => m.Grade).Include(m => m.Teacher)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

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
        if (!await db.Grades.AnyAsync(g => g.Id == gradeId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var materials = await db.LearningMaterials
            .Include(m => m.Subject).Include(m => m.Grade).Include(m => m.Teacher)
            .AsNoTracking()
            .Where(m => m.GradeId == gradeId)
            .OrderByDescending(m => m.UploadedDate)
            .Select(m => LearningMaterialDto.FromEntity(m))
            .ToListAsync();

        return Ok(materials);
    }

    // GET /api/learning-materials/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<LearningMaterialDto>>> GetBySubject(int subjectId)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var materials = await db.LearningMaterials
            .Include(m => m.Subject).Include(m => m.Grade).Include(m => m.Teacher)
            .AsNoTracking()
            .Where(m => m.SubjectId == subjectId)
            .OrderByDescending(m => m.UploadedDate)
            .Select(m => LearningMaterialDto.FromEntity(m))
            .ToListAsync();

        return Ok(materials);
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

        if (!await db.Teachers.AnyAsync(t => t.Id == teacherId))
        {
            return NotFound(new { message = "Teacher not found." });
        }

        var materials = await db.LearningMaterials
            .Include(m => m.Subject).Include(m => m.Grade).Include(m => m.Teacher)
            .AsNoTracking()
            .Where(m => m.TeacherId == teacherId)
            .OrderByDescending(m => m.UploadedDate)
            .Select(m => LearningMaterialDto.FromEntity(m))
            .ToListAsync();

        return Ok(materials);
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

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var subjectIds = await db.StudentSubjects
            .AsNoTracking()
            .Where(ss => ss.StudentId == studentId)
            .Select(ss => ss.SubjectId)
            .ToListAsync();

        var materials = await db.LearningMaterials
            .Include(m => m.Subject).Include(m => m.Grade).Include(m => m.Teacher)
            .AsNoTracking()
            .Where(m => m.GradeId == student.GradeId && subjectIds.Contains(m.SubjectId))
            .OrderByDescending(m => m.UploadedDate)
            .Select(m => LearningMaterialDto.FromEntity(m))
            .ToListAsync();

        return Ok(materials);
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

        if (!await db.Subjects.AnyAsync(s => s.Id == request.SubjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await db.Grades.AnyAsync(g => g.Id == request.GradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (!await db.Teachers.AnyAsync(t => t.Id == request.TeacherId))
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

        db.LearningMaterials.Add(material);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // The file was saved but its metadata couldn't be — clean up rather than leave an orphaned file.
            logger.LogError(ex, "Failed to save learning material metadata for {Title}; removing orphaned file", request.Title);
            await fileStorage.DeleteFileAsync(storedFileName);
            return StatusCode(500, new { message = "Unable to upload learning material." });
        }

        await db.Entry(material).Reference(m => m.Subject).LoadAsync();
        await db.Entry(material).Reference(m => m.Grade).LoadAsync();
        await db.Entry(material).Reference(m => m.Teacher).LoadAsync();
        var result = LearningMaterialDto.FromEntity(material);
        return CreatedAtAction(nameof(GetLearningMaterialById), new { id = material.Id }, result);
    }

    // GET /api/learning-materials/{id}/download
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadLearningMaterial(int id)
    {
        var material = await db.LearningMaterials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
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
        var material = await db.LearningMaterials.FirstOrDefaultAsync(m => m.Id == id);
        if (material is null)
        {
            return NotFound();
        }

        // Deleting a file that's already missing is not an error — the end state (no file) is what we want.
        await fileStorage.DeleteFileAsync(material.StoredFileName);

        db.LearningMaterials.Remove(material);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete learning material {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the learning material. Please try again." });
        }

        return NoContent();
    }
}
