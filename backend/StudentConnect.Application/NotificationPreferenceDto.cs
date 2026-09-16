namespace StudentConnect.Application;

public class NotificationPreferenceDto
{
    public bool DigestEnabled { get; set; }
    public bool EmailChannelEnabled { get; set; } = true;
    public bool InAppChannelEnabled { get; set; } = true;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
}
