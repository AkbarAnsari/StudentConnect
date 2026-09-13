using Microsoft.AspNetCore.Http;
using StudentConnect.Domain;

namespace StudentConnect.Application;

public class LearningMaterialDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedDate { get; set; }

    public static LearningMaterialDto FromEntity(LearningMaterial material) => new()
    {
        Id = material.Id,
        Title = material.Title,
        Description = material.Description,
        SubjectId = material.SubjectId,
        SubjectName = material.Subject?.Name ?? string.Empty,
        GradeId = material.GradeId,
        GradeName = material.Grade?.Name ?? string.Empty,
        TeacherId = material.TeacherId,
        TeacherName = material.Teacher is null ? string.Empty : $"{material.Teacher.FirstName} {material.Teacher.LastName}",
        FileName = material.FileName,
        ContentType = material.ContentType,
        FileSize = material.FileSize,
        UploadedDate = material.UploadedDate,
    };
}

// Bound from multipart/form-data — the file arrives alongside its metadata in one request.
public class LearningMaterialUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public int GradeId { get; set; }
    public int TeacherId { get; set; }
    public IFormFile? File { get; set; }
}
