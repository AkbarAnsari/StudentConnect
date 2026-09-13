using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Auditing;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Admin-only management of Parent accounts and which children they're linked to.
// Parents themselves never call this controller — they only use the existing
// student-scoped endpoints (attendance/marks/homework/materials/activities), which
// ICurrentUserService.CanAccessStudent already allows for a linked child.
[ApiController]
[Route("api/parents")]
[Authorize(Roles = "Admin")]
public class ParentsController(UserStore users, ParentStore parents, StudentStore students, IAuditService audit, ILogger<ParentsController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    // GET /api/parents
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParentResponseDto>>> GetParents()
    {
        var parentUsers = await users.GetParentUsersAsync();
        var links = await parents.GetAllLinksAsync();

        var result = parentUsers.Select(p => ParentResponseDto.FromEntity(
            p,
            links.Where(l => l.ParentUserId == p.Id)
                 .Select(l => new ParentChildDto
                 {
                     Id = l.StudentId,
                     StudentId = l.StudentCode,
                     FirstName = l.FirstName,
                     LastName = l.LastName,
                     GradeName = l.GradeName,
                     Section = l.Section,
                 })
                 .ToList()));

        return Ok(result);
    }

    // POST /api/parents
    [HttpPost]
    public async Task<ActionResult<ParentResponseDto>> CreateParent(ParentCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        if (await users.UsernameExistsAsync(dto.Username))
        {
            return Conflict(new { message = $"Username '{dto.Username}' is already taken." });
        }

        var distinctChildIds = dto.ChildStudentIds.Distinct().ToList();
        foreach (var studentId in distinctChildIds)
        {
            if (!await students.ExistsAsync(studentId))
            {
                return BadRequest(new { message = "One or more child student ids do not exist." });
            }
        }

        var user = new ApplicationUser { Username = dto.Username, Role = UserRole.Parent };
        var passwordHash = Hasher.HashPassword(user, dto.Password);
        var newUserId = await users.CreateAsync(dto.Username, passwordHash, UserRole.Parent);

        foreach (var studentId in distinctChildIds)
        {
            await parents.LinkChildAsync(newUserId, studentId);
        }

        logger.LogInformation("Admin created parent account {Username} linked to {Count} child(ren)", dto.Username, distinctChildIds.Count);
        await audit.LogAsync("Create", "Parent", newUserId.ToString(), $"{dto.Username}, linked to {distinctChildIds.Count} child(ren)");

        var children = await parents.GetChildrenForParentAsync(newUserId);
        user.Id = newUserId;
        return CreatedAtAction(nameof(GetParents), null, ParentResponseDto.FromEntity(user, children));
    }

    // POST /api/parents/{parentId}/children
    [HttpPost("{parentId:int}/children")]
    public async Task<IActionResult> LinkChild(int parentId, LinkChildDto dto)
    {
        if (!await users.ParentExistsAsync(parentId))
        {
            return NotFound(new { message = "Parent account not found." });
        }

        if (!await students.ExistsAsync(dto.StudentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        if (await parents.LinkExistsAsync(parentId, dto.StudentId))
        {
            return Conflict(new { message = "This parent is already linked to that student." });
        }

        await parents.LinkChildAsync(parentId, dto.StudentId);

        logger.LogInformation("Linked parent {ParentId} to student {StudentId}", parentId, dto.StudentId);
        await audit.LogAsync("Update", "Parent", parentId.ToString(), $"Linked to student {dto.StudentId}");
        return NoContent();
    }

    // DELETE /api/parents/{parentId}/children/{studentId}
    [HttpDelete("{parentId:int}/children/{studentId:int}")]
    public async Task<IActionResult> UnlinkChild(int parentId, int studentId)
    {
        var deleted = await parents.UnlinkChildAsync(parentId, studentId);
        if (deleted == 0)
        {
            return NotFound();
        }

        logger.LogInformation("Unlinked parent {ParentId} from student {StudentId}", parentId, studentId);
        await audit.LogAsync("Update", "Parent", parentId.ToString(), $"Unlinked from student {studentId}");
        return NoContent();
    }
}
