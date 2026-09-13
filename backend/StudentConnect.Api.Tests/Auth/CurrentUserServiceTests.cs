using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Auth;
using Xunit;

namespace StudentConnect.Api.Tests.Auth;

public class CurrentUserServiceTests
{
    private static ICurrentUserService CreateService(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new CurrentUserService(new FakeHttpContextAccessor(httpContext));
    }

    private sealed class FakeHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    [Fact]
    public void Admin_CanAccessAnyStudent()
    {
        var service = CreateService(new Claim(ClaimTypes.Role, "Admin"));
        Assert.True(service.CanAccessStudent(999));
    }

    [Fact]
    public void Teacher_CanAccessAnyStudent()
    {
        var service = CreateService(new Claim(ClaimTypes.Role, "Teacher"));
        Assert.True(service.CanAccessStudent(1));
    }

    [Fact]
    public void Student_CanAccessOwnRecord_ButNotOthers()
    {
        var service = CreateService(new Claim(ClaimTypes.Role, "Student"), new Claim("StudentId", "5"));
        Assert.True(service.CanAccessStudent(5));
        Assert.False(service.CanAccessStudent(6));
    }

    [Fact]
    public void Parent_CanAccessLinkedChildren_ButNotOthers()
    {
        var service = CreateService(
            new Claim(ClaimTypes.Role, "Parent"),
            new Claim("ChildStudentId", "3"),
            new Claim("ChildStudentId", "7"));

        Assert.True(service.CanAccessStudent(3));
        Assert.True(service.CanAccessStudent(7));
        Assert.False(service.CanAccessStudent(4));
    }

    [Fact]
    public void Parent_WithNoLinkedChildren_CannotAccessAnyStudent()
    {
        var service = CreateService(new Claim(ClaimTypes.Role, "Parent"));
        Assert.False(service.CanAccessStudent(1));
        Assert.Empty(service.ChildStudentIds);
    }

    [Fact]
    public void Unauthenticated_IsNotAuthenticated()
    {
        var service = new CurrentUserService(new FakeHttpContextAccessor(new DefaultHttpContext()));
        Assert.False(service.IsAuthenticated);
        Assert.False(service.CanAccessStudent(1));
    }
}
