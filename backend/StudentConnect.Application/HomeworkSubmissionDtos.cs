using Microsoft.AspNetCore.Http;
using StudentConnect.Domain;

namespace StudentConnect.Application;

public class HomeworkSubmissionDto
{
    public int Id { get; set; }
    public int HomeworkId { get; set; }
    public string HomeworkTitle { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public string? TeacherFeedback { get; set; }
    public DateTime? FeedbackAtUtc { get; set; }

    public static HomeworkSubmissionDto FromEntity(HomeworkSubmission submission) => new()
    {
        Id = submission.Id,
        HomeworkId = submission.HomeworkId,
        HomeworkTitle = submission.Homework?.Title ?? string.Empty,
        StudentId = submission.StudentId,
        StudentName = submission.Student is null ? string.Empty : $"{submission.Student.FirstName} {submission.Student.LastName}",
        SubmittedDate = submission.SubmittedDate,
        Status = submission.Status.ToString(),
        Comments = submission.Comments,
        FileName = submission.FileName,
        ContentType = submission.ContentType,
        FileSize = submission.FileSize,
        TeacherFeedback = submission.TeacherFeedback,
        FeedbackAtUtc = submission.FeedbackAtUtc,
    };
}

// What a student sends — StudentId is NOT accepted from the client; the server determines
// it from the authenticated caller (see HomeworkSubmissionsController), along with the
// submission timestamp and Submitted/Late status. Bound from multipart/form-data since File
// is optional alongside Comments.
public class HomeworkSubmissionCreateDto
{
    public int HomeworkId { get; set; }
    public string Comments { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}

public class HomeworkSubmissionUpdateDto
{
    public string Comments { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
}

public class HomeworkSubmissionFeedbackDto
{
    public string TeacherFeedback { get; set; } = string.Empty;
}

// One row of a teacher's "who submitted" view — derived by combining the class roster with submissions.
public class SubmissionOverviewDto
{
    public int? SubmissionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedDate { get; set; }
    public string? Comments { get; set; }
    public bool HasFile { get; set; }
    public string? TeacherFeedback { get; set; }
}
