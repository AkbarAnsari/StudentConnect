namespace StudentConnect.Api.Models;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Never includes the password or password hash.
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }

    // Populated for Parent-role users only — lets the UI offer a "which child?" picker
    // without a separate round-trip right after login.
    public List<ParentChildDto>? Children { get; set; }
}
