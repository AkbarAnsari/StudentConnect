using StudentConnect.Domain;

namespace StudentConnect.Application;

public class EmailOutboxDto
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public int? DeadLetterOwnerUserId { get; set; }
    public DateTime? DeadLetterAssignedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }

    public static EmailOutboxDto FromEntity(EmailOutboxEntry entry) => new()
    {
        Id = entry.Id,
        ToEmail = entry.ToEmail,
        Subject = entry.Subject,
        Body = entry.Body,
        CreatedAtUtc = entry.CreatedAtUtc,
        SentAtUtc = entry.SentAtUtc,
        Attempts = entry.Attempts,
        LastError = entry.LastError,
        DeadLetterOwnerUserId = entry.DeadLetterOwnerUserId,
        DeadLetterAssignedAtUtc = entry.DeadLetterAssignedAtUtc,
        ResolutionNotes = entry.ResolutionNotes,
    };
}

public class DeadLetterAssignmentDto { public string? ResolutionNotes { get; set; } }

public class EmailOutboxRetryRequestDto
{
    public List<int> Ids { get; set; } = [];
}
