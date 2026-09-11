using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubjectsController(StudentConnectDbContext db) : ControllerBase
{
    // GET /api/subjects — used by the (temporary, unauthenticated) Record Attendance page.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subject>>> GetSubjects()
    {
        var subjects = await db.Subjects.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        return Ok(subjects);
    }
}
