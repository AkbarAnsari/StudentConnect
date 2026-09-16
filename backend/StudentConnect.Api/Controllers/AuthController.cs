using System.Security.Cryptography;
using System.Text;
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

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(
    UserStore users,
    ParentStore parents,
    PasswordResetTokenStore resetTokens,
    EmailVerificationTokenStore verificationTokens,
    RefreshTokenStore refreshTokens,
    EmailOutboxStore emailOutbox,
    JwtTokenService tokenService,
    ICurrentUserService currentUser,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();
    private const int ResetTokenValidityMinutes = 30;
    private const int VerificationTokenValidityMinutes = 60 * 24;
    private const int RefreshTokenValidityDays = 30;

    // POST /api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto)
    {
        // Deliberately generic on both "unknown username" and "wrong password" — never
        // reveal to a caller which part of the credential was wrong.
        const string invalidCredentialsMessage = "Invalid username or password.";

        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return Unauthorized(new { message = invalidCredentialsMessage });
        }

        var user = await users.GetByUsernameForLoginAsync(dto.Username);

        if (user is null)
        {
            return Unauthorized(new { message = invalidCredentialsMessage });
        }

        if (user.Status != UserAccountStatus.Active)
        {
            return Unauthorized(new { message = invalidCredentialsMessage });
        }

        var verification = Hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = invalidCredentialsMessage });
        }

        logger.LogInformation("User {Username} logged in with role {Role}", user.Username, user.Role);
        await users.MarkLoginAsync(user.Id);

        return Ok(await BuildLoginResponseAsync(user));
    }

    // POST /api/auth/refresh — exchanges a still-valid refresh token for a new access token,
    // rotating the refresh token itself (the old one is revoked so it can't be replayed).
    [HttpPost("refresh")]
    [EnableRateLimiting("auth-relaxed")]
    public async Task<ActionResult<LoginResponseDto>> Refresh(RefreshRequestDto dto)
    {
        const string invalidTokenMessage = "Invalid or expired refresh token.";

        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            return Unauthorized(new { message = invalidTokenMessage });
        }

        var lookup = await refreshTokens.GetByTokenHashAsync(HashToken(dto.RefreshToken));
        if (lookup is null || lookup.RevokedAtUtc is not null || lookup.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = invalidTokenMessage });
        }

        var user = await users.GetByIdForLoginAsync(lookup.UserId);
        if (user is null || user.Status != UserAccountStatus.Active)
        {
            return Unauthorized(new { message = invalidTokenMessage });
        }

        // Rotate: the presented token is single-use from here on.
        await refreshTokens.RevokeAsync(lookup.Id);

        return Ok(await BuildLoginResponseAsync(user, lookup.SessionId));
    }

    // POST /api/auth/logout — best-effort server-side revocation of the refresh token so it
    // can no longer be exchanged for new access tokens (the still-live access token expires
    // naturally soon after; only the refresh token is durably revocable).
    [HttpPost("logout")]
    [EnableRateLimiting("auth-relaxed")]
    public async Task<IActionResult> Logout(LogoutRequestDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            await refreshTokens.RevokeByTokenHashAsync(HashToken(dto.RefreshToken));
        }

        return NoContent();
    }

    // POST /api/auth/verify-email — anonymous: the raw token from the emailed link is the proof.
    [HttpPost("verify-email")]
    [EnableRateLimiting("auth-relaxed")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
        {
            return BadRequest(new { message = "Token is required." });
        }

        var lookup = await verificationTokens.GetValidByTokenHashAsync(HashToken(dto.Token));
        if (lookup is null)
        {
            return BadRequest(new { message = "This verification link is invalid or has expired." });
        }

        await users.MarkEmailVerifiedAsync(lookup.UserId);
        await verificationTokens.MarkUsedAsync(lookup.Id);

        logger.LogInformation("Email verified for user {UserId}", lookup.UserId);

        return Ok(new { message = "Your email address has been verified." });
    }

    // POST /api/auth/resend-verification — for the currently logged-in account only.
    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth-relaxed")]
    public async Task<IActionResult> ResendVerification()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var email = await users.GetEmailForUserAsync(userId);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "No email address is on file for this account." });
        }

        await SendVerificationEmailAsync(userId, email);

        return Ok(new { message = "A new verification email has been sent." });
    }

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto dto)
    {
        // Always the same response whether the username exists, has no email on file, or
        // the email fails to enqueue — never reveal account existence to an anonymous caller.
        const string genericMessage = "If an account exists for that username, a password reset email has been sent.";

        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            var user = await users.GetByUsernameForLoginAsync(dto.Username);
            var email = user is null ? null : await users.GetEmailForUserAsync(user.Id);
            if (user is not null && !string.IsNullOrWhiteSpace(email))
            {
                var rawToken = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
                await resetTokens.CreateAsync(user.Id, HashToken(rawToken), DateTime.UtcNow.AddMinutes(ResetTokenValidityMinutes));

                var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
                var resetLink = $"{frontendBaseUrl}/reset-password/{rawToken}";
                await emailOutbox.EnqueueAsync(
                    email,
                    user.Id,
                    "Reset your StudentConnect password",
                    $"We received a request to reset your password. This link expires in {ResetTokenValidityMinutes} minutes and can only be used once:\n\n{resetLink}\n\nIf you didn't request this, you can ignore this email.");

                logger.LogInformation("Password reset requested for user {Username}", user.Username);
            }
        }

        return Ok(new { message = genericMessage });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return BadRequest(new { message = "Token and NewPassword are required." });
        }

        if (dto.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "NewPassword must be at least 8 characters." });
        }

        var lookup = await resetTokens.GetValidByTokenHashAsync(HashToken(dto.Token));
        if (lookup is null)
        {
            return BadRequest(new { message = "This reset link is invalid or has expired." });
        }

        var passwordHash = Hasher.HashPassword(new ApplicationUser { Id = lookup.UserId }, dto.NewPassword);
        await users.UpdatePasswordHashAsync(lookup.UserId, passwordHash);
        await resetTokens.MarkUsedAsync(lookup.Id);

        logger.LogInformation("Password reset completed for user {UserId}", lookup.UserId);

        return Ok(new { message = "Your password has been reset. You can now log in." });
    }

    [HttpGet("account")]
    public async Task<ActionResult<AccountProfileDto>> GetAccount()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId ||
            HttpContext.User.FindFirst("SchoolId")?.Value is not { } schoolValue ||
            !int.TryParse(schoolValue, out var schoolId))
        {
            return Unauthorized();
        }

        var account = await users.GetAccountAsync(userId, schoolId);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPut("account")]
    public async Task<ActionResult<AccountProfileDto>> UpdateAccount(AccountProfileUpdateDto dto)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId ||
            HttpContext.User.FindFirst("SchoolId")?.Value is not { } schoolValue ||
            !int.TryParse(schoolValue, out var schoolId))
        {
            return Unauthorized();
        }

        var email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        if (email is not null && !email.Contains('@', StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        await users.UpdateEmailAsync(userId, schoolId, email);
        return Ok((await users.GetAccountAsync(userId, schoolId))!);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("auth-relaxed")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (dto.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "New password must be at least 8 characters." });
        }

        var user = await users.GetByIdForLoginAsync(userId);
        if (user is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        await users.UpdatePasswordHashAsync(userId, Hasher.HashPassword(user, dto.NewPassword));
        await refreshTokens.RevokeAllForUserAsync(userId);
        return Ok(new { message = "Password changed. Sign in again with your new password." });
    }

    [HttpPost("revoke-sessions")]
    public async Task<IActionResult> RevokeSessions()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        await refreshTokens.RevokeAllForUserAsync(userId);
        return Ok(new { message = "All refresh sessions have been revoked." });
    }

    // Builds the full login/refresh response: issues a new access token, issues+stores a new
    // rotated refresh token, and resolves the display name — shared by Login and Refresh so
    // the two never drift out of sync.
    private async Task<LoginResponseDto> BuildLoginResponseAsync(ApplicationUser user, Guid? sessionId = null)
    {
        List<ParentChildDto>? children = null;
        List<int>? childStudentIds = null;
        if (user.Role == UserRole.Parent)
        {
            children = await parents.GetChildrenForParentAsync(user.Id);
            childStudentIds = children.Select(c => c.Id).ToList();
        }

        var accessToken = tokenService.CreateToken(user, childStudentIds);

        var rawRefreshToken = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await refreshTokens.CreateAsync(
            user.Id,
            HashToken(rawRefreshToken),
            DateTime.UtcNow.AddDays(RefreshTokenValidityDays),
            sessionId,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString());

        var displayName = user.Role switch
        {
            UserRole.Student when user.Student is not null => $"{user.Student.FirstName} {user.Student.LastName}",
            UserRole.Teacher when user.Teacher is not null => $"{user.Teacher.FirstName} {user.Teacher.LastName}",
            UserRole.Admin => "Administrator",
            UserRole.Parent => user.Username,
            _ => user.Username,
        };

        return new LoginResponseDto
        {
            Token = accessToken,
            RefreshToken = rawRefreshToken,
            Username = user.Username,
            Role = user.Role.ToString(),
            DisplayName = displayName,
            StudentId = user.StudentId,
            TeacherId = user.TeacherId,
            EmailVerified = user.EmailVerifiedAtUtc is not null,
            Children = children,
        };
    }

    // Generates a fresh verification token, stores its hash, and enqueues the verification
    // email via the outbox. Used by ResendVerification (SchoolsController.Register has its
    // own equivalent inline, since it can't depend on this controller).
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
}

