using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Auth;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Public, unauthenticated self-service tenant onboarding — creates a brand-new School and its
// first Admin user account in one call, and logs that admin in immediately. The admin's email
// is captured and a verification email is enqueued (see docs/solution-architecture-multi-tenancy.md
// for what's still deliberately deferred: platform-admin console, billing, RLS, etc.).
[ApiController]
[Route("api/schools")]
[AllowAnonymous]
public class SchoolsController(
    SchoolStore schools,
    UserStore users,
    EmailVerificationTokenStore verificationTokens,
    EmailOutboxStore emailOutbox,
    RefreshTokenStore refreshTokens,
    JwtTokenService tokenService,
    IConfiguration configuration,
    ILogger<SchoolsController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();
    private const int VerificationTokenValidityMinutes = 60 * 24;
    private const int RefreshTokenValidityDays = 30;

    // POST /api/schools/register — no auth to guard, so a per-IP limit is the only abuse
    // control against automated mass tenant creation (same "auth-relaxed" budget as the
    // other non-credential-guessing AuthController actions).
    [HttpPost("register")]
    [EnableRateLimiting("auth-relaxed")]
    public async Task<ActionResult<LoginResponseDto>> Register(SchoolRegistrationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SchoolName) ||
            string.IsNullOrWhiteSpace(dto.AdminUsername) ||
            string.IsNullOrWhiteSpace(dto.AdminPassword) ||
            string.IsNullOrWhiteSpace(dto.AdminEmail))
        {
            return BadRequest(new { message = "SchoolName, AdminUsername, AdminPassword, and AdminEmail are required." });
        }

        if (dto.AdminPassword.Length < 8)
        {
            return BadRequest(new { message = "AdminPassword must be at least 8 characters." });
        }

        if (!dto.AdminEmail.Contains('@'))
        {
            return BadRequest(new { message = "AdminEmail must be a valid email address." });
        }

        if (await users.UsernameExistsAsync(dto.AdminUsername))
        {
            return Conflict(new { message = $"Username '{dto.AdminUsername}' is already taken." });
        }

        var code = await GenerateUniqueCodeAsync(dto.SchoolName);
        var schoolId = await schools.CreateAsync(dto.SchoolName, code);

        var adminUser = new ApplicationUser { Username = dto.AdminUsername, Role = UserRole.Admin, SchoolId = schoolId };
        var passwordHash = Hasher.HashPassword(adminUser, dto.AdminPassword);
        adminUser.Id = await users.CreateAsync(dto.AdminUsername, passwordHash, UserRole.Admin, schoolId, email: dto.AdminEmail);

        var token = tokenService.CreateToken(adminUser);

        var rawRefreshToken = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await refreshTokens.CreateAsync(adminUser.Id, HashToken(rawRefreshToken), DateTime.UtcNow.AddDays(RefreshTokenValidityDays));

        await SendVerificationEmailAsync(adminUser.Id, dto.AdminEmail);

        logger.LogInformation("Self-service registration: created school '{SchoolName}' ({SchoolId}) with admin {Username}", dto.SchoolName, schoolId, dto.AdminUsername);

        return Ok(new LoginResponseDto
        {
            Token = token,
            RefreshToken = rawRefreshToken,
            Username = adminUser.Username,
            Role = adminUser.Role.ToString(),
            DisplayName = "Administrator",
            EmailVerified = false,
        });
    }

    // Mirrors AuthController's own verification-email issuance exactly (can't share a private
    // method across controllers) — generates a fresh token, stores its hash, enqueues the email.
    private async Task SendVerificationEmailAsync(int userId, string email)
    {
        var rawToken = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await verificationTokens.CreateAsync(userId, HashToken(rawToken), DateTime.UtcNow.AddMinutes(VerificationTokenValidityMinutes));

        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
        var verifyLink = $"{frontendBaseUrl}/verify-email/{rawToken}";
        await emailOutbox.EnqueueAsync(
            email,
            userId,
            "Verify your StudentConnect email address",
            $"Please confirm your email address by visiting the link below. It expires in {VerificationTokenValidityMinutes / 60} hours:\n\n{verifyLink}\n\nIf you didn't create this account, you can ignore this email.");
    }

    private static string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

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
