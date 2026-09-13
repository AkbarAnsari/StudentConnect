using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Api.Auditing;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Admin-only management of Teacher records and (optionally) their login accounts.
[ApiController]
[Route("api/teachers")]
[Authorize(Roles = "Admin")]
public class TeachersController(TeacherStore teachers, UserStore users, IAuditService audit, ILogger<TeachersController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    // GET /api/teachers
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeacherResponseDto>>> GetTeachers()
    {
        var all = await teachers.GetAllAsync();
        return Ok(all);
    }

    // POST /api/teachers
    [HttpPost]
    public async Task<ActionResult<TeacherResponseDto>> CreateTeacher(TeacherCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TeacherId) ||
            string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "TeacherId, FirstName, LastName, and Email are required." });
        }

        var wantsLogin = !string.IsNullOrWhiteSpace(dto.Username) || !string.IsNullOrWhiteSpace(dto.Password);
        if (wantsLogin && (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password)))
        {
            return BadRequest(new { message = "Username and password must both be provided to create a login." });
        }

        if (await teachers.TeacherCodeExistsAsync(dto.TeacherId))
        {
            return Conflict(new { message = $"A teacher with TeacherId '{dto.TeacherId}' already exists." });
        }

        if (wantsLogin && await users.UsernameExistsAsync(dto.Username!))
        {
            return Conflict(new { message = $"Username '{dto.Username}' is already taken." });
        }

        int newId;
        try
        {
            newId = await teachers.CreateAsync(new Teacher
            {
                TeacherId = dto.TeacherId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
            });
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save new teacher {TeacherId}", dto.TeacherId);
            return StatusCode(500, new { message = "Unable to save the teacher. Please try again." });
        }

        if (wantsLogin)
        {
            var user = new ApplicationUser { Username = dto.Username!, Role = UserRole.Teacher };
            var passwordHash = Hasher.HashPassword(user, dto.Password!);
            await users.CreateAsync(dto.Username!, passwordHash, UserRole.Teacher, teacherId: newId);
        }

        logger.LogInformation("Admin created teacher {TeacherId} (login: {HasLogin})", dto.TeacherId, wantsLogin);
        await audit.LogAsync("Create", "Teacher", newId.ToString(), $"{dto.TeacherId} {dto.FirstName} {dto.LastName} (login: {wantsLogin})");

        return CreatedAtAction(nameof(GetTeachers), null, TeacherResponseDto.FromEntity(
            new Teacher { Id = newId, TeacherId = dto.TeacherId, FirstName = dto.FirstName, LastName = dto.LastName, Email = dto.Email },
            wantsLogin ? dto.Username : null));
    }

    // DELETE /api/teachers/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTeacher(int id)
    {
        if (!await teachers.ExistsAsync(id))
        {
            return NotFound();
        }

        try
        {
            await teachers.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete teacher {Id}", id);
            return Conflict(new { message = "Cannot delete a teacher who has homework, materials, attendance records, or a login account associated with them." });
        }

        await audit.LogAsync("Delete", "Teacher", id.ToString());
        return NoContent();
    }
}
