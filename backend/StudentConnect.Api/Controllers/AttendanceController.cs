using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;
using StudentConnect.Api.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController(StudentConnectDbContext db, ICurrentUserService currentUser, INotificationService notifications, ILogger<AttendanceController> logger) : ControllerBase
{
    // GET /api/attendance
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetAttendance()
    {
        var records = await db.Attendance
            .Include(a => a.Student)
            .Include(a => a.Subject)
            .AsNoTracking()
            .OrderByDescending(a => a.Date)
            .Select(a => AttendanceRecordDto.FromEntity(a))
            .ToListAsync();

        return Ok(records);
    }

    // GET /api/attendance/student/{studentId} — a Student may only view their own attendance.
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetStudentAttendance(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var records = await db.Attendance
            .Include(a => a.Subject)
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.Date)
            .Select(a => AttendanceRecordDto.FromEntity(a))
            .ToListAsync();

        return Ok(records);
    }

    // GET /api/attendance/student/{studentId}/subject/{subjectId}
    [HttpGet("student/{studentId:int}/subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetStudentSubjectAttendance(int studentId, int subjectId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var records = await db.Attendance
            .Include(a => a.Subject)
            .AsNoTracking()
            .Where(a => a.StudentId == studentId && a.SubjectId == subjectId)
            .OrderByDescending(a => a.Date)
            .Select(a => AttendanceRecordDto.FromEntity(a))
            .ToListAsync();

        return Ok(records);
    }

    // GET /api/attendance/student/{studentId}/summary
    [HttpGet("student/{studentId:int}/summary")]
    public async Task<ActionResult<IEnumerable<AttendanceSummaryDto>>> GetStudentAttendanceSummary(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var summary = await db.StudentSubjects
            .AsNoTracking()
            .Where(ss => ss.StudentId == studentId)
            .OrderBy(ss => ss.SubjectId)
            .Select(ss => new AttendanceSummaryDto
            {
                SubjectId = ss.SubjectId,
                SubjectName = ss.Subject!.Name,
                TotalClasses = db.Attendance.Count(a => a.StudentId == studentId && a.SubjectId == ss.SubjectId),
                Present = db.Attendance.Count(a => a.StudentId == studentId && a.SubjectId == ss.SubjectId && a.Status == AttendanceStatus.Present),
            })
            .ToListAsync();

        // Percentage is calculated here, on the backend — never in Angular.
        foreach (var item in summary)
        {
            item.Absent = item.TotalClasses - item.Present;
            item.Percentage = item.TotalClasses == 0 ? 0 : Math.Round(item.Present * 100.0 / item.TotalClasses, 0);
        }

        return Ok(summary);
    }

    // POST /api/attendance
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<AttendanceRecordDto>> CreateAttendance(AttendanceCreateDto dto)
    {
        if (!TryParseStatus(dto.Status, out var status))
        {
            return BadRequest(new { message = "Status must be 'Present' or 'Absent'." });
        }

        if (!await db.Students.AnyAsync(s => s.Id == dto.StudentId))
        {
            return BadRequest(new { message = "Student does not exist." });
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == dto.SubjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        var duplicate = await db.Attendance.AnyAsync(a =>
            a.StudentId == dto.StudentId && a.SubjectId == dto.SubjectId && a.Date == dto.Date);
        if (duplicate)
        {
            return Conflict(new { message = "An attendance record already exists for this student, subject, and date." });
        }

        var attendance = new Attendance
        {
            StudentId = dto.StudentId,
            SubjectId = dto.SubjectId,
            Date = dto.Date,
            Status = status,
        };

        db.Attendance.Add(attendance);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save attendance for student {StudentId}", dto.StudentId);
            return StatusCode(500, new { message = "Unable to save the attendance record. Please try again." });
        }

        await db.Entry(attendance).Reference(a => a.Student).LoadAsync();
        await db.Entry(attendance).Reference(a => a.Subject).LoadAsync();

        if (status == AttendanceStatus.Absent)
        {
            await NotifyAbsence(attendance.StudentId, attendance.Subject!.Name, attendance.Date);
        }

        var result = AttendanceRecordDto.FromEntity(attendance);
        return CreatedAtAction(nameof(GetStudentAttendance), new { studentId = attendance.StudentId }, result);
    }

    // POST /api/attendance/bulk — a teacher saving attendance for a whole class at once.
    // Existing (student, subject, date) records are corrected in place instead of rejected as duplicates.
    [HttpPost("bulk")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> CreateBulkAttendance(BulkAttendanceRequestDto dto)
    {
        if (dto.Records.Count == 0)
        {
            return BadRequest(new { message = "At least one attendance record is required." });
        }

        if (!await db.Subjects.AnyAsync(s => s.Id == dto.SubjectId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        var studentIds = dto.Records.Select(r => r.StudentId).Distinct().ToList();
        var existingStudentIds = await db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.Id).ToListAsync();
        var missingStudentIds = studentIds.Except(existingStudentIds).ToList();
        if (missingStudentIds.Count > 0)
        {
            return BadRequest(new { message = $"Unknown student id(s): {string.Join(", ", missingStudentIds)}" });
        }

        var parsedRecords = new List<(int StudentId, AttendanceStatus Status)>();
        foreach (var record in dto.Records)
        {
            if (!TryParseStatus(record.Status, out var status))
            {
                return BadRequest(new { message = $"Invalid status '{record.Status}' for student {record.StudentId}." });
            }

            parsedRecords.Add((record.StudentId, status));
        }

        var existing = await db.Attendance
            .Where(a => a.SubjectId == dto.SubjectId && a.Date == dto.Date && studentIds.Contains(a.StudentId))
            .ToListAsync();
        var existingByStudent = existing.ToDictionary(a => a.StudentId);

        foreach (var (studentId, status) in parsedRecords)
        {
            if (existingByStudent.TryGetValue(studentId, out var record))
            {
                record.Status = status;
            }
            else
            {
                db.Attendance.Add(new Attendance { StudentId = studentId, SubjectId = dto.SubjectId, Date = dto.Date, Status = status });
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save bulk attendance for subject {SubjectId} on {Date}", dto.SubjectId, dto.Date);
            return StatusCode(500, new { message = "Unable to save attendance. Please try again." });
        }

        var subjectName = (await db.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.SubjectId))?.Name ?? "a subject";
        foreach (var (studentId, status) in parsedRecords.Where(r => r.Status == AttendanceStatus.Absent))
        {
            await NotifyAbsence(studentId, subjectName, dto.Date);
        }

        return Ok(new { message = "Attendance saved successfully.", count = parsedRecords.Count });
    }

    // PUT /api/attendance/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<AttendanceRecordDto>> UpdateAttendance(int id, AttendanceUpdateDto dto)
    {
        if (!TryParseStatus(dto.Status, out var status))
        {
            return BadRequest(new { message = "Status must be 'Present' or 'Absent'." });
        }

        var attendance = await db.Attendance
            .Include(a => a.Student)
            .Include(a => a.Subject)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attendance is null)
        {
            return NotFound();
        }

        attendance.Status = status;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update attendance {Id}", id);
            return StatusCode(500, new { message = "Unable to update the attendance record. Please try again." });
        }

        return Ok(AttendanceRecordDto.FromEntity(attendance));
    }

    // DELETE /api/attendance/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteAttendance(int id)
    {
        var attendance = await db.Attendance.FindAsync(id);
        if (attendance is null)
        {
            return NotFound();
        }

        db.Attendance.Remove(attendance);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete attendance {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the attendance record. Please try again." });
        }

        return NoContent();
    }

    private static bool TryParseStatus(string? status, out AttendanceStatus result) =>
        Enum.TryParse(status, ignoreCase: true, out result) && Enum.IsDefined(result);

    // Notifies the student's own account plus any linked parents — same recipient
    // resolution a real email/SMS provider would use later.
    private async Task NotifyAbsence(int studentId, string subjectName, DateOnly date)
    {
        var recipientUserIds = await GetStudentAndParentUserIds(studentId);
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        await notifications.NotifyManyAsync(
            recipientUserIds,
            "Absence recorded",
            $"Marked absent for {subjectName} on {date:yyyy-MM-dd}.",
            NotificationType.AttendanceAbsent);
    }

    private async Task<List<int>> GetStudentAndParentUserIds(int studentId)
    {
        var studentUserIds = await db.Users
            .Where(u => u.StudentId == studentId)
            .Select(u => u.Id)
            .ToListAsync();

        var parentUserIds = await db.ParentStudents
            .Where(ps => ps.StudentId == studentId)
            .Select(ps => ps.ParentUserId)
            .ToListAsync();

        return studentUserIds.Concat(parentUserIds).Distinct().ToList();
    }
}
