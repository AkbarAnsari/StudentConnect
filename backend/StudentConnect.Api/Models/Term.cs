namespace StudentConnect.Api.Models;

// A grading period within an AcademicYear (e.g. "Term 1").
public class Term
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
