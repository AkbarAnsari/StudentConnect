namespace StudentConnect.Application;

public class CurriculumDefaultDto
{
    public int GradeId { get; set; }
    public int? SectionId { get; set; }
    public List<int> SubjectIds { get; set; } = [];
}

public class CurriculumDefaultReplaceDto
{
    public int GradeId { get; set; }
    public int? SectionId { get; set; }
    public List<int> SubjectIds { get; set; } = [];
}