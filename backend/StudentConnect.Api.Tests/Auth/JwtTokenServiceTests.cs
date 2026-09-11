using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Models;
using Xunit;

namespace StudentConnect.Api.Tests.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() =>
        new(Options.Create(new JwtOptions
        {
            Secret = "test-only-signing-key-at-least-32-characters-long",
            Issuer = "Test",
            Audience = "Test",
            ExpiryMinutes = 60,
        }));

    [Fact]
    public void CreateToken_IncludesRoleAndStudentIdClaims()
    {
        var service = CreateService();
        var user = new ApplicationUser { Id = 1, Username = "student1", Role = UserRole.Student, StudentId = 42 };

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        Assert.Equal("Student", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == "StudentId").Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "TeacherId");
    }

    [Fact]
    public void CreateToken_ForParent_IncludesOneChildStudentIdClaimPerChild()
    {
        var service = CreateService();
        var user = new ApplicationUser { Id = 9, Username = "parent1", Role = UserRole.Parent };

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user, childStudentIds: [3, 7]));

        var childClaims = jwt.Claims.Where(c => c.Type == "ChildStudentId").Select(c => c.Value).OrderBy(v => v).ToList();
        Assert.Equal(["3", "7"], childClaims);
    }

    [Fact]
    public void CreateToken_WithNoChildren_AddsNoChildStudentIdClaims()
    {
        var service = CreateService();
        var user = new ApplicationUser { Id = 2, Username = "admin1", Role = UserRole.Admin };

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateToken(user));

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "ChildStudentId");
    }
}
