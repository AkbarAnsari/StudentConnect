using StudentConnect.Domain;

namespace StudentConnect.Application;

public class ActivityDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int CurrentRegistrations { get; set; }
    public int AvailableSeats { get; set; }
    public int CurrentWaitlisted { get; set; }
    public int? SectionId { get; set; }
    public string? SectionName { get; set; }
    public int? OwnerUserId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public DateTime? RegistrationOpensAtUtc { get; set; }
    public DateTime? RegistrationClosesAtUtc { get; set; }
    public bool ConsentRequired { get; set; }
    public decimal? FeeAmount { get; set; }
    public string ModerationStatus { get; set; } = string.Empty;
    public string? ModerationReason { get; set; }
    public DateTime? ModeratedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public static ActivityDto FromEntity(Activity activity, int currentRegistrations, int currentWaitlisted = 0) => new()
    {
        Id = activity.Id,
        Title = activity.Title,
        Description = activity.Description,
        Date = activity.Date,
        Location = activity.Location,
        GradeId = activity.GradeId,
        GradeName = activity.Grade?.Name ?? string.Empty,
        Capacity = activity.Capacity,
        CurrentRegistrations = currentRegistrations,
        AvailableSeats = Math.Max(0, activity.Capacity - currentRegistrations),
        CurrentWaitlisted = currentWaitlisted,
        SectionId = activity.SectionId,
        SectionName = activity.Section?.Name,
        OwnerUserId = activity.OwnerUserId,
        OrganizerName = activity.OrganizerName,
        ContactEmail = activity.ContactEmail,
        RegistrationOpensAtUtc = activity.RegistrationOpensAtUtc,
        RegistrationClosesAtUtc = activity.RegistrationClosesAtUtc,
        ConsentRequired = activity.ConsentRequired,
        FeeAmount = activity.FeeAmount,
        ModerationStatus = activity.ModerationStatus,
        ModerationReason = activity.ModerationReason,
        ModeratedAtUtc = activity.ModeratedAtUtc,
        Status = activity.Status.ToString(),
        CancelledAtUtc = activity.CancelledAtUtc,
        CompletedAtUtc = activity.CompletedAtUtc,
    };
}

public class ActivityCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public int Capacity { get; set; }
    public int? SectionId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public DateTime? RegistrationOpensAtUtc { get; set; }
    public DateTime? RegistrationClosesAtUtc { get; set; }
    public bool ConsentRequired { get; set; }
    public decimal? FeeAmount { get; set; }
    public string Status { get; set; } = "Draft";
}

// Grade is fixed after creation — changing it would silently invalidate existing
// registrations' eligibility, the same reasoning Homework's update DTO already follows.
public class ActivityUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? SectionId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public DateTime? RegistrationOpensAtUtc { get; set; }
    public DateTime? RegistrationClosesAtUtc { get; set; }
    public bool ConsentRequired { get; set; }
    public decimal? FeeAmount { get; set; }
}

public class ActivityStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}

public class ActivityModerationDto
{
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
