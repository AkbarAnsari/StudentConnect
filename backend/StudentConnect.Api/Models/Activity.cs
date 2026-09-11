namespace StudentConnect.Api.Models;

// The activity itself — capacity is a limit, not a running counter (active registrations are counted, never stored).
public class Activity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int Capacity { get; set; }
    public DateTime CreatedDate { get; set; }
}
