namespace StudentConnect.Domain;

// One attendance record = a Student + Subject + Date + Status.
// Business rule: a student can only have one record per subject per date (enforced by a unique index).
public class Attendance
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }
}
