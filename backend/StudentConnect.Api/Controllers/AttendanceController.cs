using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController(
    AttendanceStore attendance,
    StudentStore students,
    ReferenceDataStore referenceData,
    NotificationRecipientResolver recipients,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    INotificationService notifications,
    IAuditService audit,
    ILogger<AttendanceController> logger) : ControllerBase
{
    // GET /api/attendance
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetAttendance()
    {
        var records = await attendance.GetAllAsync();
        return Ok(records.Select(AttendanceRecordDto.FromEntity));
    }

    // GET /api/attendance/subject/{subjectId}/date/{date} — every already-recorded status for
    // that subject+date, used to prefill the Record Attendance grid before a bulk save.
    [HttpGet("subject/{subjectId:int}/date/{date}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetBySubjectAndDate(int subjectId, DateOnly date)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.SubjectExistsAsync(subjectId, schoolId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var records = await attendance.GetBySubjectAndDateAsync(subjectId, date);
        return Ok(records.Select(AttendanceRecordDto.FromEntity));
    }

    // GET /api/attendance/student/{studentId} — a Student may only view their own attendance.
    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetStudentAttendance(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var records = await attendance.GetByStudentAsync(studentId);
        return Ok(records.Select(AttendanceRecordDto.FromEntity));
    }

    // GET /api/attendance/student/{studentId}/subject/{subjectId}
    [HttpGet("student/{studentId:int}/subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<AttendanceRecordDto>>> GetStudentSubjectAttendance(int studentId, int subjectId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        if (!await referenceData.SubjectExistsAsync(subjectId, schoolId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var records = await attendance.GetByStudentAndSubjectAsync(studentId, subjectId);
        return Ok(records.Select(AttendanceRecordDto.FromEntity));
    }

    // GET /api/attendance/student/{studentId}/summary
    [HttpGet("student/{studentId:int}/summary")]
    public async Task<ActionResult<IEnumerable<AttendanceSummaryDto>>> GetStudentAttendanceSummary(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(studentId, schoolId))
        {
            return NotFound(new { message = "Student not found." });
        }

        // Percentage is calculated here, on the backend — never in Angular.
        var summary = await attendance.GetSummaryAsync(studentId);
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

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await students.ExistsAsync(dto.StudentId, schoolId))
        {
            return BadRequest(new { message = "Student does not exist." });
        }

        if (!await referenceData.SubjectExistsAsync(dto.SubjectId, schoolId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (await attendance.ExistsForStudentSubjectDateAsync(dto.StudentId, dto.SubjectId, dto.Date))
        {
            return Conflict(new { message = "An attendance record already exists for this student, subject, and date." });
        }

        int newId;
        try
        {
            newId = await attendance.CreateAsync(new Attendance { StudentId = dto.StudentId, SubjectId = dto.SubjectId, Date = dto.Date, Status = status });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save attendance for student {StudentId}", dto.StudentId);
            return StatusCode(500, new { message = "Unable to save the attendance record. Please try again." });
        }

        var created = await attendance.GetByIdAsync(newId);

        if (status == AttendanceStatus.Absent)
        {
            await NotifyAbsence(dto.StudentId, created!.Subject!.Name, dto.Date);
        }

        await audit.LogAsync("Create", "Attendance", newId.ToString(), $"StudentId={dto.StudentId}, SubjectId={dto.SubjectId}, Date={dto.Date}, Status={status}");
        var result = AttendanceRecordDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetStudentAttendance), new { studentId = dto.StudentId }, result);
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

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var subject = await referenceData.GetSubjectByIdAsync(dto.SubjectId, schoolId);
        if (subject is null)
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        var studentIds = dto.Records.Select(r => r.StudentId).Distinct().ToList();
        var missingStudentIds = new List<int>();
        foreach (var studentId in studentIds)
        {
            if (!await students.ExistsAsync(studentId, schoolId))
            {
                missingStudentIds.Add(studentId);
            }
        }
        if (missingStudentIds.Count > 0)
        {
            return BadRequest(new { message = $"Unknown student id(s): {string.Join(", ", missingStudentIds)}" });
        }

        var parsedRecords = new List<(int StudentId, AttendanceStatus Status, string? Reason)>();
        foreach (var record in dto.Records)
        {
            if (!TryParseStatus(record.Status, out var status))
            {
                return BadRequest(new { message = $"Invalid status '{record.Status}' for student {record.StudentId}." });
            }

            parsedRecords.Add((record.StudentId, status, record.Reason));
        }

        var existingByStudent = await attendance.GetExistingForSubjectDateAsync(dto.SubjectId, dto.Date, studentIds);

        try
        {
            foreach (var (studentId, status, reason) in parsedRecords)
            {
                if (existingByStudent.TryGetValue(studentId, out var existingRecord))
                {
                    // Only overwrite when the status actually changed, so a reason isn't
                    // required/stamped on rows that were re-saved unchanged.
                    if (existingRecord.Status != status)
                    {
                        await attendance.UpdateStatusAsync(existingRecord.Id, status, reason);
                    }
                }
                else
                {
                    await attendance.CreateAsync(new Attendance { StudentId = studentId, SubjectId = dto.SubjectId, Date = dto.Date, Status = status });
                }
            }
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save bulk attendance for subject {SubjectId} on {Date}", dto.SubjectId, dto.Date);
            return StatusCode(500, new { message = "Unable to save attendance. Please try again." });
        }

        foreach (var (studentId, status, _) in parsedRecords.Where(r => r.Status == AttendanceStatus.Absent))
        {
            await NotifyAbsence(studentId, subject.Name, dto.Date);
        }

        await audit.LogAsync("Update", "Attendance", $"subject:{dto.SubjectId}:date:{dto.Date}", $"Bulk save for {parsedRecords.Count} student(s)");
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

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { message = "A reason is required when correcting an attendance record." });
        }

        var record = await attendance.GetByIdAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(record.StudentId, schoolId))
        {
            return NotFound();
        }

        try
        {
            await attendance.UpdateStatusAsync(id, status, dto.Reason);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update attendance {Id}", id);
            return StatusCode(500, new { message = "Unable to update the attendance record. Please try again." });
        }

        await audit.LogAsync("Update", "Attendance", id.ToString(), $"Status: {record.Status} -> {status}; Reason: {dto.Reason}");
        record.Status = status;
        record.CorrectionReason = dto.Reason;
        return Ok(AttendanceRecordDto.FromEntity(record));
    }

    // DELETE /api/attendance/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteAttendance(int id)
    {
        var record = await attendance.GetByIdAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(record.StudentId, schoolId))
        {
            return NotFound();
        }

        try
        {
            await attendance.DeleteAsync(id);
        }
        catch (PostgresException ex)
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
        var recipientUserIds = await recipients.ForStudentAsync(studentId);
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
}
