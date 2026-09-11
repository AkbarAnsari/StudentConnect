using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradesController(StudentConnectDbContext db) : ControllerBase
{
    // GET /api/grades — used by Angular's Add/Edit Student forms to populate the grade dropdown.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Grade>>> GetGrades()
    {
        var grades = await db.Grades.AsNoTracking().OrderBy(g => g.Id).ToListAsync();
        return Ok(grades);
    }
}
