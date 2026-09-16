namespace StudentConnect.Application;

public class LeaveRequestCreateDto
{
    public int StudentId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class LeaveRequestDecisionDto
{
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateOnly LeaveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DecisionReason { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}