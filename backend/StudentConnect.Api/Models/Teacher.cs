namespace StudentConnect.Api.Models;

// Exists so Homework can record who created it — no teacher authentication yet.
public class Teacher
{
    public int Id { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
