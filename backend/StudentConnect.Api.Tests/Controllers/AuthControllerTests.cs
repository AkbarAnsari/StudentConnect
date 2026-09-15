using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Auth;
using StudentConnect.Infrastructure.Data.Stores;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class AuthControllerTests(DatabaseFixture fixture)
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    private AuthController CreateController() =>
        new(
            new UserStore(fixture.Database),
            new ParentStore(fixture.Database),
            new PasswordResetTokenStore(fixture.Database),
            new EmailVerificationTokenStore(fixture.Database),
            new RefreshTokenStore(fixture.Database),
            new EmailOutboxStore(fixture.Database),
            new JwtTokenService(Options.Create(new JwtOptions
            {
                Secret = "test-only-signing-secret-not-for-production-use-1234567890",
                Issuer = "StudentConnectTest",
                Audience = "StudentConnectTest",
            })),
            TestCurrentUser.Create(),
            new ConfigurationBuilder().Build(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthController>.Instance);

    private async Task<(int UserId, string Username, string Password)> SeedUserAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        const string password = "Password123!";
        var hash = Hasher.HashPassword(new ApplicationUser(), password);

        var userId = await new UserStore(fixture.Database).CreateAsync(username, hash, UserRole.Admin, schoolId: 1);
        return (userId, username, password);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndRefreshToken()
    {
        var (_, username, password) = await SeedUserAsync();
        var controller = CreateController();

        var result = await controller.Login(new LoginRequestDto { Username = username, Password = password });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LoginResponseDto>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(dto.Token));
        Assert.False(string.IsNullOrWhiteSpace(dto.RefreshToken));
        Assert.Equal("Admin", dto.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsGenericUnauthorizedMessage()
    {
        var (_, username, _) = await SeedUserAsync();
        var controller = CreateController();

        var result = await controller.Login(new LoginRequestDto { Username = username, Password = "WrongPassword!" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUsername_ReturnsSameGenericMessageAsWrongPassword()
    {
        var controller = CreateController();

        var unknownResult = await controller.Login(new LoginRequestDto { Username = "does-not-exist", Password = "whatever" });
        var (_, username, _) = await SeedUserAsync();
        var wrongPasswordResult = await controller.Login(new LoginRequestDto { Username = username, Password = "wrong" });

        var unknownUnauthorized = Assert.IsType<UnauthorizedObjectResult>(unknownResult.Result);
        var wrongPasswordUnauthorized = Assert.IsType<UnauthorizedObjectResult>(wrongPasswordResult.Result);
        Assert.Equal(unknownUnauthorized.Value!.ToString(), wrongPasswordUnauthorized.Value!.ToString());
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenCannotBeReused()
    {
        var (_, username, password) = await SeedUserAsync();
        var controller = CreateController();
        var loginResult = await controller.Login(new LoginRequestDto { Username = username, Password = password });
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginDto = Assert.IsType<LoginResponseDto>(loginOk.Value);

        var firstRefresh = await controller.Refresh(new RefreshRequestDto { RefreshToken = loginDto.RefreshToken! });
        Assert.IsType<OkObjectResult>(firstRefresh.Result);

        var secondRefresh = await controller.Refresh(new RefreshRequestDto { RefreshToken = loginDto.RefreshToken! });
        Assert.IsType<UnauthorizedObjectResult>(secondRefresh.Result);
    }

    [Fact]
    public async Task ForgotPassword_UnknownUsername_StillReturnsGenericOk()
    {
        var controller = CreateController();

        var result = await controller.ForgotPassword(new ForgotPasswordRequestDto { Username = "no-such-user" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ResetPassword(new ResetPasswordRequestDto { Token = "not-a-real-token", NewPassword = "NewPassword123!" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_AllowsLoginWithNewPassword()
    {
        var (userId, username, _) = await SeedUserAsync();
        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        await new PasswordResetTokenStore(fixture.Database).CreateAsync(userId, tokenHash, DateTime.UtcNow.AddMinutes(30));
        var controller = CreateController();

        var resetResult = await controller.ResetPassword(new ResetPasswordRequestDto { Token = rawToken, NewPassword = "BrandNewPassword123!" });
        Assert.IsType<OkObjectResult>(resetResult);

        var loginResult = await controller.Login(new LoginRequestDto { Username = username, Password = "BrandNewPassword123!" });
        Assert.IsType<OkObjectResult>(loginResult.Result);
    }
}
