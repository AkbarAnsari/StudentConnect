namespace StudentConnect.Domain;

// One row of the outbox pattern table — enqueued immediately, sent later by
// EmailOutboxProcessor. See schema.sql "EmailOutbox" for the column definitions.
public class EmailOutboxEntry
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public int? ToUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public int? DeadLetterOwnerUserId { get; set; }
    public DateTime? DeadLetterAssignedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }
}
