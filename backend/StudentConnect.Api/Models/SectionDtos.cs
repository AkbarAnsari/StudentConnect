namespace StudentConnect.Api.Models;

public class SectionDto
{
    public int Id { get; set; }
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public static SectionDto FromEntity(Section section) => new()
    {
        Id = section.Id,
        GradeId = section.GradeId,
        GradeName = section.Grade?.Name ?? string.Empty,
        Name = section.Name,
    };
}

public class SectionCreateDto
{
    public int GradeId { get; set; }
    public string Name { get; set; } = string.Empty;
}
