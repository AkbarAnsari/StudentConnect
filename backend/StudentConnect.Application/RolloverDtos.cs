namespace StudentConnect.Application;

// One row of the academic-year rollover preview — the default Grade/Section mapping
// StudentConnect proposes for a student, before an Admin confirms (or overrides) it.
// See AcademicYearsController's rollover-preview/rollover-commit endpoints.
public class RolloverStudentDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int CurrentGradeId { get; set; }
    public string CurrentGradeName { get; set; } = string.Empty;
    public int? CurrentSectionId { get; set; }
    public string? CurrentSectionName { get; set; }
    public int? ProposedGradeId { get; set; }
    public string? ProposedGradeName { get; set; }
    public int? ProposedSectionId { get; set; }
    public string? ProposedSectionName { get; set; }
    public bool Excluded { get; set; }
    public string? ExclusionReason { get; set; }
}

// What Angular sends to commit a rollover — an optional per-student override list; students
// not listed use the previewed default mapping.
public class RolloverCommitRequestDto
{
    public List<RolloverOverrideDto> Overrides { get; set; } = [];
}

public class RolloverOverrideDto
{
    public int StudentId { get; set; }
    public bool Skip { get; set; }
    public int? GradeId { get; set; }
    public int? SectionId { get; set; }
}
