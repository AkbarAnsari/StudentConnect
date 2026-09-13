namespace StudentConnect.Api.Models;

public class AcademicYearDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public static AcademicYearDto FromEntity(AcademicYear year) => new()
    {
        Id = year.Id,
        Name = year.Name,
        StartDate = year.StartDate,
        EndDate = year.EndDate,
        IsCurrent = year.IsCurrent,
    };
}

public class AcademicYearCreateDto
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
}
