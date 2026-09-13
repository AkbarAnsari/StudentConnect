using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Auth;

public class CurrentTenantService(IHttpContextAccessor httpContextAccessor) : ICurrentTenantService
{
    public int? SchoolId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst("SchoolId")?.Value;
            return int.TryParse(value, out var result) ? result : null;
        }
    }
}
