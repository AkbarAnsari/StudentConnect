namespace StudentConnect.Domain;

// The relationship itself carries information (RegistrationDate, Status) — this is why
// Student <-> Activity many-to-many needs its own entity rather than a simple list.
// Business rule: one row per student+activity — re-registering after cancellation flips
// Status back to Registered instead of inserting a second row (enforced by a unique index).
public class ActivityRegistration
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int ActivityId { get; set; }
    public Activity? Activity { get; set; }

    // Set by the server at registration time — never trusted from the client.
    public DateTime RegistrationDate { get; set; }
    public ActivityRegistrationStatus Status { get; set; }
    public bool ConsentConfirmed { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public int? CheckedInByUserId { get; set; }
    public string FeeStatus { get; set; } = "NotApplicable";
    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentReference { get; set; }
    public string? ConsentDocumentFileName { get; set; }
    public string? ConsentDocumentStoredFileName { get; set; }
    public string? ConsentDocumentContentType { get; set; }
    public long? ConsentDocumentSize { get; set; }
    public DateTime? ConsentDocumentUploadedAtUtc { get; set; }
}
