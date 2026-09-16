namespace StudentConnect.Application;

public class AccountProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}

public class AccountProfileUpdateDto
{
    public string? Email { get; set; }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class UserAccessDto : AccountProfileDto
{
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
}

public class UserInvitationCreateDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
}

public class ManagedContactUpdateDto
{
    public string? Email { get; set; }
}

public class UserSessionDto
{
    public Guid SessionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}