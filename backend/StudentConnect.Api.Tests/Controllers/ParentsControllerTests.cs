using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using StudentConnect.Api.Controllers;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture). Usernames are randomized
// per test run since they persist in a shared database across runs (unlike the old EF Core
// InMemory provider, which started from a blank database every time).
[Collection("Database")]
public class ParentsControllerTests(DatabaseFixture fixture)
{
    private ParentsController CreateController() =>
        new(new UserStore(fixture.Database), new ParentStore(fixture.Database), new StudentStore(fixture.Database), NullAuditService.Instance, NullLogger<ParentsController>.Instance);

    private static string UniqueUsername(string prefix) => $"{prefix}.{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task CreateParent_LinksRequestedChildren()
    {
        var controller = CreateController();
        var username = UniqueUsername("parent-test");

        var result = await controller.CreateParent(new ParentCreateDto
        {
            Username = username,
            Password = "P@ssw0rd!",
            ChildStudentIds = [1],
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ParentResponseDto>(created.Value);

        Assert.Equal(username, dto.Username);
        Assert.Single(dto.Children);
        Assert.Equal(1, dto.Children[0].Id);
    }

    [Fact]
    public async Task CreateParent_DuplicateUsername_ReturnsConflict()
    {
        var controller = CreateController();
        var username = UniqueUsername("dup-parent");

        await controller.CreateParent(new ParentCreateDto { Username = username, Password = "P@ssw0rd!" });
        var second = await controller.CreateParent(new ParentCreateDto { Username = username, Password = "AnotherP@ss1" });

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task LinkChild_ThenUnlinkChild_RemovesTheLink()
    {
        var controller = CreateController();
        const int studentId = 2;

        var createResult = await controller.CreateParent(new ParentCreateDto { Username = UniqueUsername("link-test"), Password = "P@ssw0rd!" });
        var parentDto = Assert.IsType<ParentResponseDto>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var linkResult = await controller.LinkChild(parentDto.Id, new LinkChildDto { StudentId = studentId });
        Assert.IsType<NoContentResult>(linkResult);

        var duplicateLink = await controller.LinkChild(parentDto.Id, new LinkChildDto { StudentId = studentId });
        Assert.IsType<ConflictObjectResult>(duplicateLink);

        var unlinkResult = await controller.UnlinkChild(parentDto.Id, studentId);
        Assert.IsType<NoContentResult>(unlinkResult);

        var stillLinked = await new ParentStore(fixture.Database).LinkExistsAsync(parentDto.Id, studentId);
        Assert.False(stillLinked);
    }
}
