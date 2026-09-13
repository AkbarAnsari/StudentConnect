namespace StudentConnect.Domain;

// "NotSubmitted" is intentionally NOT a value here — a missing submission record
// already means "not submitted"; storing that as a row would be redundant data.
public enum HomeworkSubmissionStatus
{
    Submitted,
    Late,
}
