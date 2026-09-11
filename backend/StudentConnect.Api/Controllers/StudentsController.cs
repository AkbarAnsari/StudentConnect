using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController(StudentConnectDbContext db, ICurrentUserService currentUser, ILogger<StudentsController> logger) : ControllerBase
{
    // GET /api/students
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetStudents()
    {
        var students = await db.Students
            .Include(s => s.Grade)
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => StudentResponseDto.FromEntity(s))
            .ToListAsync();

        return Ok(students);
    }

    // GET /api/students/{id} — a Student may only view their own profile; Teacher/Admin may view any.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentResponseDto>> GetStudentById(int id)
    {
        if (!currentUser.CanAccessStudent(id))
        {
            return Forbid();
        }

        var student = await db.Students
            .Include(s => s.Grade)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null)
        {
            return NotFound();
        }

        return Ok(StudentResponseDto.FromEntity(student));
    }

    // POST /api/students
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> CreateStudent(StudentCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StudentId) ||
            string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "StudentId, FirstName, LastName, and Email are required." });
        }

        var gradeExists = await db.Grades.AnyAsync(g => g.Id == dto.GradeId);
        if (!gradeExists)
        {
            return BadRequest(new { message = "The specified GradeId does not exist." });
        }

        var duplicate = await db.Students.AnyAsync(s => s.StudentId == dto.StudentId);
        if (duplicate)
        {
            return Conflict(new { message = $"A student with StudentId '{dto.StudentId}' already exists." });
        }

        var student = new Student
        {
            StudentId = dto.StudentId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            GradeId = dto.GradeId,
            Section = dto.Section,
        };

        db.Students.Add(student);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save new student {StudentId}", dto.StudentId);
            return StatusCode(500, new { message = "Unable to save the student. Please try again." });
        }

        await db.Entry(student).Reference(s => s.Grade).LoadAsync();
        var result = StudentResponseDto.FromEntity(student);
        return CreatedAtAction(nameof(GetStudentById), new { id = student.Id }, result);
    }

    // PUT /api/students/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> UpdateStudent(int id, StudentUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "FirstName, LastName, and Email are required." });
        }

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null)
        {
            return NotFound();
        }

        var gradeExists = await db.Grades.AnyAsync(g => g.Id == dto.GradeId);
        if (!gradeExists)
        {
            return BadRequest(new { message = "The specified GradeId does not exist." });
        }

        student.FirstName = dto.FirstName;
        student.LastName = dto.LastName;
        student.Email = dto.Email;
        student.GradeId = dto.GradeId;
        student.Section = dto.Section;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to update student {Id}", id);
            return StatusCode(500, new { message = "Unable to update the student. Please try again." });
        }

        await db.Entry(student).Reference(s => s.Grade).LoadAsync();
        return Ok(StudentResponseDto.FromEntity(student));
    }

    // DELETE /api/students/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null)
        {
            return NotFound();
        }

        db.Students.Remove(student);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to delete student {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the student. Please try again." });
        }

        return NoContent();
    }
}

