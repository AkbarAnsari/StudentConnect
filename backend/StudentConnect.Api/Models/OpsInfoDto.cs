namespace StudentConnect.Api.Models;

public class OpsInfoDto
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; }
    public double UptimeSeconds { get; set; }
    public bool DatabaseHealthy { get; set; }
    public long PendingEmailCount { get; set; }
    public long FailedEmailCount { get; set; }
}
