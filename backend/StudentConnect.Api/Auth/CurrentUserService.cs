using System.Security.Claims;

namespace StudentConnect.Api.Auth;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UserId => TryGetInt(ClaimTypes.NameIdentifier);

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);

    public int? StudentId => TryGetInt("StudentId");

    public int? TeacherId => TryGetInt("TeacherId");

    public bool IsTeacherOrAdmin => Role is "Teacher" or "Admin";

    public bool CanAccessStudent(int studentId) => IsTeacherOrAdmin || StudentId == studentId;

    public bool CanActAsTeacher(int teacherId) => Role == "Admin" || TeacherId == teacherId;

    private int? TryGetInt(string claimType)
    {
        var value = User?.FindFirstValue(claimType);
        return int.TryParse(value, out var result) ? result : null;
    }
}
