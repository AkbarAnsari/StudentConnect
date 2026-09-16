using StudentConnect.Domain;

namespace StudentConnect.Application;

public class OpsInfoDto
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; }
    public double UptimeSeconds { get; set; }
    public bool DatabaseHealthy { get; set; }
    public double DatabaseResponseMilliseconds { get; set; }
    public long PendingEmailCount { get; set; }
    public long FailedEmailCount { get; set; }
    public long SentEmailCountLast24Hours { get; set; }
    public DateTime? OldestPendingEmailAtUtc { get; set; }
    public DateTime? LastEmailSentAtUtc { get; set; }
    public DateTime? LastEmailFailureAtUtc { get; set; }
    public string OverallStatus { get; set; } = string.Empty;
    public string? RunbookUrl { get; set; }
}

public class OperationalSettingsDto
{
    public string? AlertEmail { get; set; }
    public string? AlertWebhookUrl { get; set; }
    public int FailedEmailThreshold { get; set; } = 5;
    public int AuditRetentionDays { get; set; } = 365;
    public int NotificationRetentionDays { get; set; } = 180;
    public bool RedactAuditDetails { get; set; } = true;
    public string? RunbookUrl { get; set; }
}
