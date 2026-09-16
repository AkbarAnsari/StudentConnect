using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Auth;

namespace StudentConnect.Api.Tests;

// Builds a real CurrentUserService from claims, for controller tests that need role/teacherId/
// studentId-aware authorization (mirrors the pattern in Auth/CurrentUserServiceTests.cs).
public static class TestCurrentUser
{
    private sealed class FakeHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    public static ICurrentUserService Create(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new CurrentUserService(new FakeHttpContextAccessor(httpContext));
    }

    // NameIdentifier must reference a row that actually exists in "Users" — several store
    // methods stamp it into NOT NULL foreign-key columns (e.g. AttendanceCorrections/Marks
    // audit trails), so an arbitrary id would fail with a Postgres FK violation instead of
    // the intended authorization behavior. These ids (admin1=4, teacher1=3, student1=1) are
    // guaranteed present by seed.sql's "ON CONFLICT (\"Id\") DO NOTHING" upsert.
    public static ICurrentUserService Admin() =>
        Create(new Claim(ClaimTypes.NameIdentifier, "4"), new Claim(ClaimTypes.Role, "Admin"));

    public static ICurrentUserService Teacher(int teacherId) =>
        Create(new Claim(ClaimTypes.NameIdentifier, "3"), new Claim(ClaimTypes.Role, "Teacher"), new Claim("TeacherId", teacherId.ToString()));

    public static ICurrentUserService Student(int studentId) =>
        Create(new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Student"), new Claim("StudentId", studentId.ToString()));
}
