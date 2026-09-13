using StudentConnect.Domain;

namespace StudentConnect.Application;

public class EmailOutboxDto
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public static EmailOutboxDto FromEntity(EmailOutboxEntry entry) => new()
    {
        Id = entry.Id,
        ToEmail = entry.ToEmail,
        Subject = entry.Subject,
        CreatedAtUtc = entry.CreatedAtUtc,
        SentAtUtc = entry.SentAtUtc,
        Attempts = entry.Attempts,
        LastError = entry.LastError,
    };
}
