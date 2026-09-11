namespace StudentConnect.Api.Models;

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

    public static ActivityDto FromEntity(Activity activity, int currentRegistrations) => new()
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
}
