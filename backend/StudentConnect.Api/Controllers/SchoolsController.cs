using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Auth;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Public, unauthenticated self-service tenant onboarding — creates a brand-new School and its
// first Admin user account in one call, and logs that admin in immediately. No email
// verification in this pass — see docs/solution-architecture-multi-tenancy.md for what's
// deliberately deferred (platform-admin console, billing, RLS, etc.).
[ApiController]
[Route("api/schools")]
[AllowAnonymous]
public class SchoolsController(SchoolStore schools, UserStore users, JwtTokenService tokenService, ILogger<SchoolsController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    // POST /api/schools/register
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponseDto>> Register(SchoolRegistrationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SchoolName) ||
            string.IsNullOrWhiteSpace(dto.AdminUsername) ||
            string.IsNullOrWhiteSpace(dto.AdminPassword))
        {
            return BadRequest(new { message = "SchoolName, AdminUsername, and AdminPassword are required." });
        }

        if (dto.AdminPassword.Length < 8)
        {
            return BadRequest(new { message = "AdminPassword must be at least 8 characters." });
        }

        if (await users.UsernameExistsAsync(dto.AdminUsername))
        {
            return Conflict(new { message = $"Username '{dto.AdminUsername}' is already taken." });
        }

        var code = await GenerateUniqueCodeAsync(dto.SchoolName);
        var schoolId = await schools.CreateAsync(dto.SchoolName, code);

        var adminUser = new ApplicationUser { Username = dto.AdminUsername, Role = UserRole.Admin, SchoolId = schoolId };
        var passwordHash = Hasher.HashPassword(adminUser, dto.AdminPassword);
        adminUser.Id = await users.CreateAsync(dto.AdminUsername, passwordHash, UserRole.Admin, schoolId);

        var token = tokenService.CreateToken(adminUser);

        logger.LogInformation("Self-service registration: created school '{SchoolName}' ({SchoolId}) with admin {Username}", dto.SchoolName, schoolId, dto.AdminUsername);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = adminUser.Username,
            Role = adminUser.Role.ToString(),
            DisplayName = "Administrator",
        });
    }

    private async Task<string> GenerateUniqueCodeAsync(string schoolName)
    {
        var baseSlug = Slugify(schoolName);
        var candidate = baseSlug;
        var suffix = 1;
        while (await schools.CodeExistsAsync(candidate))
        {
            suffix++;
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var slug = Regex.Replace(lowered, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "school" : slug;
    }
}
