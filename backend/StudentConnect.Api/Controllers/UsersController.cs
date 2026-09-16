using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController(
    UserStore users,
    RefreshTokenStore refreshTokens,
    PasswordResetTokenStore resetTokens,
    EmailOutboxStore emailOutbox,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IAuditService audit,
    IConfiguration configuration) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserAccessDto>>> GetUsers()
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        return Ok(await users.GetUsersForSchoolAsync(schoolId));
    }

    [HttpPost("invitations")]
    public async Task<ActionResult<UserAccessDto>> CreateInvitation(UserInvitationCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var username = (dto.Username ?? string.Empty).Trim();
        var email = (dto.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            return BadRequest(new { message = "A username and valid email are required." });
        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest(new { message = "Role must be Admin, Teacher, Student, or Parent." });
        if (await users.UsernameExistsAsync(username))
            return Conflict(new { message = $"Username '{username}' is already taken." });

        int? studentId = null;
        int? teacherId = null;
        if (role == UserRole.Student)
        {
            if (dto.StudentId is not { } requestedStudentId || !await users.CanLinkStudentAsync(requestedStudentId, schoolId))
                return BadRequest(new { message = "Select a student in this school who does not already have an account." });
            studentId = requestedStudentId;
        }
        else if (role == UserRole.Teacher)
        {
            if (dto.TeacherId is not { } requestedTeacherId || !await users.CanLinkTeacherAsync(requestedTeacherId, schoolId))
                return BadRequest(new { message = "Select a teacher in this school who does not already have an account." });
            teacherId = requestedTeacherId;
        }

        var user = new ApplicationUser { Username = username, Role = role };
        var randomPassword = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var newUserId = await users.CreateAsync(username, Hasher.HashPassword(user, randomPassword), role, schoolId, studentId, teacherId, email);
        await SendInvitationAsync(newUserId, email);
        await audit.LogAsync("Create", "User", newUserId.ToString(), $"Invited {username} as {role}");
        return CreatedAtAction(nameof(GetUsers), null, (await users.GetUsersForSchoolAsync(schoolId)).First(item => item.Id == newUserId));
    }

    [HttpPut("{id:int}/contact")]
    public async Task<ActionResult<UserAccessDto>> UpdateContact(int id, ManagedContactUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var target = (await users.GetUsersForSchoolAsync(schoolId)).FirstOrDefault(item => item.Id == id);
        if (target is null) return NotFound();
        var email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        if (email is not null && !email.Contains('@', StringComparison.Ordinal))
            return BadRequest(new { message = "Enter a valid email address." });
        await users.UpdateEmailAsync(id, schoolId, email);
        await audit.LogAsync("Update", "User", id.ToString(), $"Managed contact changed for {target.Username}");
        return Ok((await users.GetUsersForSchoolAsync(schoolId)).First(item => item.Id == id));
    }

    [HttpGet("{id:int}/sessions")]
    public async Task<ActionResult<IEnumerable<UserSessionDto>>> GetSessions(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (!(await users.GetUsersForSchoolAsync(schoolId)).Any(item => item.Id == id)) return NotFound();
        return Ok(await refreshTokens.GetActiveSessionsAsync(id));
    }

    [HttpDelete("{id:int}/sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(int id, Guid sessionId)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (!(await users.GetUsersForSchoolAsync(schoolId)).Any(item => item.Id == id)) return NotFound();
        if (await refreshTokens.RevokeSessionAsync(id, sessionId) == 0) return NotFound();
        await audit.LogAsync("RevokeSession", "User", id.ToString(), sessionId.ToString());
        return NoContent();
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (currentUser.UserId == id) return Conflict(new { message = "You cannot deactivate your own account." });
        var target = (await users.GetUsersForSchoolAsync(schoolId)).FirstOrDefault(user => user.Id == id);
        if (target is null) return NotFound();
        if (target.Role == "Admin" && await users.CountActiveAdminsAsync(schoolId) <= 1)
            return Conflict(new { message = "The last active administrator cannot be deactivated." });

        await users.SetStatusAsync(id, schoolId, UserAccountStatus.Deactivated);
        await refreshTokens.RevokeAllForUserAsync(id);
        await audit.LogAsync("Deactivate", "User", id.ToString(), target.Username);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (!await users.SetStatusAsync(id, schoolId, UserAccountStatus.Active)) return NotFound();
        await audit.LogAsync("Reactivate", "User", id.ToString());
        return NoContent();
    }

    [HttpPost("{id:int}/password-invitation")]
    public async Task<IActionResult> SendPasswordInvitation(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var target = await users.GetAccountAsync(id, schoolId);
        if (target is null) return NotFound();
        if (string.IsNullOrWhiteSpace(target.Email)) return BadRequest(new { message = "No email address is on file for this account." });

        await SendInvitationAsync(id, target.Email);
        await audit.LogAsync("Invite", "User", id.ToString(), "Password setup invitation sent");
        return Ok(new { message = "Password setup invitation sent." });
    }

    private async Task SendInvitationAsync(int userId, string email)
    {
        var rawToken = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        await resetTokens.CreateAsync(userId, tokenHash, DateTime.UtcNow.AddHours(24));
        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
        await emailOutbox.EnqueueAsync(email, userId, "Set your StudentConnect password",
            $"Use this one-time link within 24 hours to set your password:\n\n{frontendBaseUrl}/reset-password/{rawToken}");
    }
}