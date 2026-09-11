using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Admin-only management of Parent accounts and which children they're linked to.
// Parents themselves never call this controller — they only use the existing
// student-scoped endpoints (attendance/marks/homework/materials/activities), which
// ICurrentUserService.CanAccessStudent already allows for a linked child.
[ApiController]
[Route("api/parents")]
[Authorize(Roles = "Admin")]
public class ParentsController(StudentConnectDbContext db, ILogger<ParentsController> logger) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    // GET /api/parents
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParentResponseDto>>> GetParents()
    {
        var parents = await db.Users
            .Where(u => u.Role == UserRole.Parent)
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync();

        var links = await db.ParentStudents
            .Include(ps => ps.Student).ThenInclude(s => s!.Grade)
            .AsNoTracking()
            .ToListAsync();

        var result = parents.Select(p => ParentResponseDto.FromEntity(
            p,
            links.Where(l => l.ParentUserId == p.Id)
                 .Select(l => new ParentChildDto
                 {
                     Id = l.Student!.Id,
                     StudentId = l.Student.StudentId,
                     FirstName = l.Student.FirstName,
                     LastName = l.Student.LastName,
                     GradeName = l.Student.Grade!.Name,
                     Section = l.Student.Section,
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

        if (await db.Users.AnyAsync(u => u.Username == dto.Username))
        {
            return Conflict(new { message = $"Username '{dto.Username}' is already taken." });
        }

        if (dto.ChildStudentIds.Count > 0)
        {
            var existingCount = await db.Students.CountAsync(s => dto.ChildStudentIds.Contains(s.Id));
            if (existingCount != dto.ChildStudentIds.Distinct().Count())
            {
                return BadRequest(new { message = "One or more child student ids do not exist." });
            }
        }

        var user = new ApplicationUser { Username = dto.Username, Role = UserRole.Parent };
        user.PasswordHash = Hasher.HashPassword(user, dto.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        foreach (var studentId in dto.ChildStudentIds.Distinct())
        {
            db.ParentStudents.Add(new ParentStudent { ParentUserId = user.Id, StudentId = studentId });
        }
        await db.SaveChangesAsync();

        logger.LogInformation("Admin created parent account {Username} linked to {Count} child(ren)", user.Username, dto.ChildStudentIds.Count);

        var children = await db.ParentStudents
            .Where(ps => ps.ParentUserId == user.Id)
            .Include(ps => ps.Student).ThenInclude(s => s!.Grade)
            .AsNoTracking()
            .Select(ps => new ParentChildDto
            {
                Id = ps.Student!.Id,
                StudentId = ps.Student.StudentId,
                FirstName = ps.Student.FirstName,
                LastName = ps.Student.LastName,
                GradeName = ps.Student.Grade!.Name,
                Section = ps.Student.Section,
            })
            .ToListAsync();

        return CreatedAtAction(nameof(GetParents), null, ParentResponseDto.FromEntity(user, children));
    }

    // POST /api/parents/{parentId}/children
    [HttpPost("{parentId:int}/children")]
    public async Task<IActionResult> LinkChild(int parentId, LinkChildDto dto)
    {
        var parent = await db.Users.FirstOrDefaultAsync(u => u.Id == parentId && u.Role == UserRole.Parent);
        if (parent is null)
        {
            return NotFound(new { message = "Parent account not found." });
        }

        if (!await db.Students.AnyAsync(s => s.Id == dto.StudentId))
        {
            return NotFound(new { message = "Student not found." });
        }

        var alreadyLinked = await db.ParentStudents.AnyAsync(ps => ps.ParentUserId == parentId && ps.StudentId == dto.StudentId);
        if (alreadyLinked)
        {
            return Conflict(new { message = "This parent is already linked to that student." });
        }

        db.ParentStudents.Add(new ParentStudent { ParentUserId = parentId, StudentId = dto.StudentId });
        await db.SaveChangesAsync();

        logger.LogInformation("Linked parent {ParentId} to student {StudentId}", parentId, dto.StudentId);
        return NoContent();
    }

    // DELETE /api/parents/{parentId}/children/{studentId}
    [HttpDelete("{parentId:int}/children/{studentId:int}")]
    public async Task<IActionResult> UnlinkChild(int parentId, int studentId)
    {
        var link = await db.ParentStudents.FirstOrDefaultAsync(ps => ps.ParentUserId == parentId && ps.StudentId == studentId);
        if (link is null)
        {
            return NotFound();
        }

        db.ParentStudents.Remove(link);
        await db.SaveChangesAsync();

        logger.LogInformation("Unlinked parent {ParentId} from student {StudentId}", parentId, studentId);
        return NoContent();
    }
}
