namespace StudentConnect.Domain;

// The activity itself — capacity is a limit, not a running counter (active registrations are counted, never stored).
public class Activity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }
    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    public int Capacity { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? OwnerUserId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public DateTime? RegistrationOpensAtUtc { get; set; }
    public DateTime? RegistrationClosesAtUtc { get; set; }
    public bool ConsentRequired { get; set; }
    public decimal? FeeAmount { get; set; }
    public string ModerationStatus { get; set; } = "Approved";
    public string? ModerationReason { get; set; }
    public int? ModeratedByUserId { get; set; }
    public DateTime? ModeratedAtUtc { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.Open;
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
