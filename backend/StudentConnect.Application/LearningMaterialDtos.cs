using Microsoft.AspNetCore.Http;
using StudentConnect.Domain;

namespace StudentConnect.Application;

public class LearningMaterialDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedDate { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }

    public static LearningMaterialDto FromEntity(LearningMaterial material) => new()
    {
        Id = material.Id,
        Title = material.Title,
        Description = material.Description,
        Topic = material.Topic,
        SubjectId = material.SubjectId,
        SubjectName = material.Subject?.Name ?? string.Empty,
        GradeId = material.GradeId,
        GradeName = material.Grade?.Name ?? string.Empty,
        SectionId = material.SectionId,
        SectionName = material.Section?.Name,
        TeacherId = material.TeacherId,
        TeacherName = material.Teacher is null ? string.Empty : $"{material.Teacher.FirstName} {material.Teacher.LastName}",
        FileName = material.FileName,
        ContentType = material.ContentType,
        FileSize = material.FileSize,
        UploadedDate = material.UploadedDate,
        Version = material.Version,
        Status = material.Status.ToString(),
        ScheduledForUtc = material.ScheduledForUtc,
        PublishedAtUtc = material.PublishedAtUtc,
        ClosedAtUtc = material.ClosedAtUtc,
        ArchivedAtUtc = material.ArchivedAtUtc,
    };
}

// Bound from multipart/form-data — the file arrives alongside its metadata in one request.
public class LearningMaterialUploadRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public int GradeId { get; set; }
    public int SectionId { get; set; }
    public int TeacherId { get; set; }
    public string Status { get; set; } = "Published";
    public DateTime? ScheduledForUtc { get; set; }
    public IFormFile? File { get; set; }
}

public class LearningMaterialReplacementRequest
{
    public string ChangeReason { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}

public class LearningMaterialVersionDto
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public string? UploadedByName { get; set; }
    public string? ChangeReason { get; set; }
}
