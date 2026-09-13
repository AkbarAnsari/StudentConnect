using StudentConnect.Domain;

namespace StudentConnect.Application;

public class ActivityRegistrationDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int ActivityId { get; set; }
    public string ActivityTitle { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime RegistrationDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public static ActivityRegistrationDto FromEntity(ActivityRegistration registration) => new()
    {
        Id = registration.Id,
        StudentId = registration.StudentId,
        StudentName = registration.Student is null ? string.Empty : $"{registration.Student.FirstName} {registration.Student.LastName}",
        ActivityId = registration.ActivityId,
        ActivityTitle = registration.Activity?.Title ?? string.Empty,
        ActivityDate = registration.Activity?.Date ?? default,
        Location = registration.Activity?.Location ?? string.Empty,
        RegistrationDate = registration.RegistrationDate,
        Status = registration.Status.ToString(),
    };
}

// What Angular sends — StudentId is NOT accepted from the client; the server determines
// it from the authenticated caller (see ActivityRegistrationsController), along with
// RegistrationDate and Status.
public class ActivityRegistrationCreateDto
{
    public int ActivityId { get; set; }
}
