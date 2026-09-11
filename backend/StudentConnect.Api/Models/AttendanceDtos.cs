namespace StudentConnect.Api.Models;

// What Angular receives — combines Student/Subject names so it never needs raw foreign keys alone.
public class AttendanceRecordDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;

    public static AttendanceRecordDto FromEntity(Attendance attendance) => new()
    {
        Id = attendance.Id,
        StudentId = attendance.StudentId,
        StudentName = attendance.Student is null ? string.Empty : $"{attendance.Student.FirstName} {attendance.Student.LastName}",
        SubjectId = attendance.SubjectId,
        SubjectName = attendance.Subject?.Name ?? string.Empty,
        Date = attendance.Date,
        Status = attendance.Status.ToString(),
    };
}

// What Angular sends to record one attendance record.
public class AttendanceCreateDto
{
    public int StudentId { get; set; }
    public int SubjectId { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
}

// What Angular sends to correct an attendance record (e.g. Absent -> Present).
public class AttendanceUpdateDto
{
    public string Status { get; set; } = string.Empty;
}

// Per-subject attendance percentage — calculated on the backend, never in Angular.
public class AttendanceSummaryDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int TotalClasses { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public double Percentage { get; set; }
}

// A teacher saving attendance for a whole class in one request.
public class BulkAttendanceRequestDto
{
    public int SubjectId { get; set; }
    public DateOnly Date { get; set; }
    public List<BulkAttendanceItemDto> Records { get; set; } = [];
}

public class BulkAttendanceItemDto
{
    public int StudentId { get; set; }
    public string Status { get; set; } = string.Empty;
}
