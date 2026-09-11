using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradesController(ReferenceDataStore referenceData) : ControllerBase
{
    // GET /api/grades — used by Angular's Add/Edit Student forms to populate the grade dropdown.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Grade>>> GetGrades()
    {
        var grades = await referenceData.GetAllGradesAsync();
        return Ok(grades);
    }
}
