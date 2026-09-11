using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudentConnect.Api.Controllers;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

public class ParentsControllerTests
{
    private static StudentConnectDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<StudentConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new StudentConnectDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static ParentsController CreateController(StudentConnectDbContext db) =>
        new(db, NullLogger<ParentsController>.Instance);

    [Fact]
    public async Task CreateParent_LinksRequestedChildren()
    {
        await using var db = CreateDb();
        var student = await db.Students.FirstAsync();
        var controller = CreateController(db);

        var result = await controller.CreateParent(new ParentCreateDto
        {
            Username = "parent.test",
            Password = "P@ssw0rd!",
            ChildStudentIds = [student.Id],
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ParentResponseDto>(created.Value);

        Assert.Equal("parent.test", dto.Username);
        Assert.Single(dto.Children);
        Assert.Equal(student.Id, dto.Children[0].Id);
    }

    [Fact]
    public async Task CreateParent_DuplicateUsername_ReturnsConflict()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        await controller.CreateParent(new ParentCreateDto { Username = "dup.parent", Password = "P@ssw0rd!" });
        var second = await controller.CreateParent(new ParentCreateDto { Username = "dup.parent", Password = "AnotherP@ss1" });

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task LinkChild_ThenUnlinkChild_RemovesTheLink()
    {
        await using var db = CreateDb();
        var student = await db.Students.Skip(1).FirstAsync();
        var controller = CreateController(db);

        var createResult = await controller.CreateParent(new ParentCreateDto { Username = "link.test", Password = "P@ssw0rd!" });
        var parentDto = Assert.IsType<ParentResponseDto>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var linkResult = await controller.LinkChild(parentDto.Id, new LinkChildDto { StudentId = student.Id });
        Assert.IsType<NoContentResult>(linkResult);

        var duplicateLink = await controller.LinkChild(parentDto.Id, new LinkChildDto { StudentId = student.Id });
        Assert.IsType<ConflictObjectResult>(duplicateLink);

        var unlinkResult = await controller.UnlinkChild(parentDto.Id, student.Id);
        Assert.IsType<NoContentResult>(unlinkResult);

        var afterUnlink = await db.ParentStudents.CountAsync(ps => ps.ParentUserId == parentDto.Id);
        Assert.Equal(0, afterUnlink);
    }
}
