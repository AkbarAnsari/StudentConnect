using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Auth;

public class JwtOptions
{
    // Development-only signing key — never a real production secret. See docs/phase-10.md.
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "StudentConnect";
    public string Audience { get; set; } = "StudentConnect";
    public int ExpiryMinutes { get; set; } = 120;
}

// Issues the access token a login produces, and nothing else — token *validation* is handled
// by the standard ASP.NET Core JWT bearer middleware configured in Program.cs.
public class JwtTokenService(IOptions<JwtOptions> options)
{
    public string CreateToken(ApplicationUser user, IEnumerable<int>? childStudentIds = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        if (user.StudentId is not null)
        {
            claims.Add(new Claim("StudentId", user.StudentId.Value.ToString()));
        }

        if (user.TeacherId is not null)
        {
            claims.Add(new Claim("TeacherId", user.TeacherId.Value.ToString()));
        }

        if (childStudentIds is not null)
        {
            claims.AddRange(childStudentIds.Select(id => new Claim("ChildStudentId", id.ToString())));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Value.Issuer,
            audience: options.Value.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.Value.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
