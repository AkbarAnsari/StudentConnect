namespace StudentConnect.Domain;

// A single school year (e.g. "2026-2027"). Exactly one should be IsCurrent at a time —
// enforced by application logic, not a DB constraint, to keep the schema simple.
public class AcademicYear
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public int SchoolId { get; set; }

    // Set once this year's students have been promoted — see AcademicYearsController rollover.
    public DateTime? RolledOverAtUtc { get; set; }

    // Archived (soft-deleted) instead of hard-deleted, so historical Terms/rollover records
    // stay intact — see AcademicYearsController.
    public bool IsArchived { get; set; }
}
