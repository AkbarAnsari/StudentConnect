using StudentConnect.Domain;

namespace StudentConnect.Application;

public class TeacherResponseDto
{
    public int Id { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }

    public static TeacherResponseDto FromEntity(Teacher teacher, string? username) => new()
    {
        Id = teacher.Id,
        TeacherId = teacher.TeacherId,
        FirstName = teacher.FirstName,
        LastName = teacher.LastName,
        Email = teacher.Email,
        Username = username,
    };
}

// Username/Password are optional — a Teacher record can be created for record-keeping
// only (e.g. a substitute) without granting them a login.
public class TeacherCreateDto
{
    public string TeacherId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
