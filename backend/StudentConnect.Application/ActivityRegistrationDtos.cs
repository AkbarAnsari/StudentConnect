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
    public bool ConsentConfirmed { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public string FeeStatus { get; set; } = string.Empty;
    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentReference { get; set; }
    public string? ConsentDocumentFileName { get; set; }
    public DateTime? ConsentDocumentUploadedAtUtc { get; set; }

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
        ConsentConfirmed = registration.ConsentConfirmed,
        CheckedInAtUtc = registration.CheckedInAtUtc,
        FeeStatus = registration.FeeStatus,
        PaidAtUtc = registration.PaidAtUtc,
        PaymentReference = registration.PaymentReference,
        ConsentDocumentFileName = registration.ConsentDocumentFileName,
        ConsentDocumentUploadedAtUtc = registration.ConsentDocumentUploadedAtUtc,
    };
}

// What Angular sends — StudentId is NOT accepted from the client; the server determines
// it from the authenticated caller (see ActivityRegistrationsController), along with
// RegistrationDate and Status.
public class ActivityRegistrationCreateDto
{
    public int ActivityId { get; set; }
    public bool ConsentConfirmed { get; set; }
}

public class ActivityCheckInDto
{
    public bool CheckedIn { get; set; }
}

public class ActivityPaymentDto
{
    public string PaymentReference { get; set; } = string.Empty;
}

public class ActivityParticipantMessageDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string RegistrationStatus { get; set; } = "Registered";
}
