namespace StudentConnect.Api.Models;

// A student's response to one homework assignment.
// Business rule: a student can only have one submission per homework (enforced by a unique index).
public class HomeworkSubmission
{
    public int Id { get; set; }

    public int HomeworkId { get; set; }
    public Homework? Homework { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    // Set by the server at submission time — never trusted from the client.
    public DateTime SubmittedDate { get; set; }
    public HomeworkSubmissionStatus Status { get; set; }
    public string Comments { get; set; } = string.Empty;
}
