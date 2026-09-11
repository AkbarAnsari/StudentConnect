using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubjectsController(ReferenceDataStore referenceData) : ControllerBase
{
    // GET /api/subjects — used by the (temporary, unauthenticated) Record Attendance page.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subject>>> GetSubjects()
    {
        var subjects = await referenceData.GetAllSubjectsAsync();
        return Ok(subjects);
    }
}
