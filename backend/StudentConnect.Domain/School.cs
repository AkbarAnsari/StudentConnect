namespace StudentConnect.Domain;

// The multi-tenancy root — every school using the product is exactly one row here. Users,
// Students, Teachers, Grades, Subjects, and AcademicYears all carry a SchoolId; Terms and
// Sections are scoped transitively through their AcademicYear/Grade instead of a redundant
// column. See docs/solution-architecture-multi-tenancy.md.
public class School
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // A URL-safe slug, unique across all schools (reserved for future subdomain-based
    // resolution — not used for anything yet).
    public string Code { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    // A soft "suspend this tenant" switch for future platform-admin use — not enforced
    // anywhere yet in this pass.
    public bool IsActive { get; set; } = true;
}
