using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Auth;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(UserStore users, ParentStore parents, JwtTokenService tokenService, ILogger<AuthController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    // POST /api/auth/login
    [HttpPost("login")]
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

        var verification = Hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = invalidCredentialsMessage });
        }

        List<ParentChildDto>? children = null;
        List<int>? childStudentIds = null;
        if (user.Role == UserRole.Parent)
        {
            children = await parents.GetChildrenForParentAsync(user.Id);
            childStudentIds = children.Select(c => c.Id).ToList();
        }

        var token = tokenService.CreateToken(user, childStudentIds);
        var displayName = user.Role switch
        {
            UserRole.Student when user.Student is not null => $"{user.Student.FirstName} {user.Student.LastName}",
            UserRole.Teacher when user.Teacher is not null => $"{user.Teacher.FirstName} {user.Teacher.LastName}",
            UserRole.Admin => "Administrator",
            UserRole.Parent => user.Username,
            _ => user.Username,
        };

        logger.LogInformation("User {Username} logged in with role {Role}", user.Username, user.Role);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role.ToString(),
            DisplayName = displayName,
            StudentId = user.StudentId,
            TeacherId = user.TeacherId,
            Children = children,
        });
    }
}

