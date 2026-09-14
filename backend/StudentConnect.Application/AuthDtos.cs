using StudentConnect.Domain;

namespace StudentConnect.Application;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Never includes the password or password hash.
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    // Opaque, long-lived, rotated on every use — exchanged via /api/auth/refresh for a new
    // access token without asking the user to log in again.
    public string RefreshToken { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
    public bool EmailVerified { get; set; }

    // Populated for Parent-role users only — lets the UI offer a "which child?" picker
    // without a separate round-trip right after login.
    public List<ParentChildDto>? Children { get; set; }
}

public class ForgotPasswordRequestDto
{
    public string Username { get; set; } = string.Empty;
}

public class ResetPasswordRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class RefreshRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class VerifyEmailRequestDto
{
    public string Token { get; set; } = string.Empty;
}
