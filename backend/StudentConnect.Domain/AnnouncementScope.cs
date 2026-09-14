namespace StudentConnect.Domain;

// Who an Announcement fans out to \u2014 resolved into Notification recipients at creation time.
public enum AnnouncementScope
{
    School,
    Grade,
    Section,
}
