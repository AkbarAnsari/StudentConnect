namespace StudentConnect.Api.Models;

public class TermDto
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public static TermDto FromEntity(Term term) => new()
    {
        Id = term.Id,
        AcademicYearId = term.AcademicYearId,
        AcademicYearName = term.AcademicYear?.Name ?? string.Empty,
        Name = term.Name,
        StartDate = term.StartDate,
        EndDate = term.EndDate,
    };
}

public class TermCreateDto
{
    public int AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
