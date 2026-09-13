namespace StudentConnect.Api.Models;

// Admin-only request to create a Parent account, optionally pre-linked to children.
public class ParentCreateDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<int> ChildStudentIds { get; set; } = [];
}

public class ParentChildDto
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
}

public class ParentResponseDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<ParentChildDto> Children { get; set; } = [];

    public static ParentResponseDto FromEntity(ApplicationUser user, List<ParentChildDto> children) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        Children = children,
    };
}

public class LinkChildDto
{
    public int StudentId { get; set; }
}
