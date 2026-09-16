using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/report-templates")]
[Authorize(Roles="Admin")]
public class ReportTemplatesController(ReportTemplateStore templates,ICurrentTenantService currentTenant):ControllerBase
{
    [HttpGet] public async Task<ActionResult<IEnumerable<ReportTemplateDto>>> Get(){if(currentTenant.SchoolId is not{} schoolId)return Forbid();return Ok(await templates.GetAllAsync(schoolId));}
    [HttpPost] public async Task<ActionResult<ReportTemplateDto>> Save(ReportTemplateSaveDto dto){if(currentTenant.SchoolId is not{} schoolId)return Forbid();if(string.IsNullOrWhiteSpace(dto.Name)||string.IsNullOrWhiteSpace(dto.HeaderText)||string.IsNullOrWhiteSpace(dto.PrimarySignatureLabel))return BadRequest(new{message="Name, header, and primary signature label are required."});try{var id=await templates.SaveAsync(dto,schoolId);return Ok(await templates.GetByIdAsync(id,schoolId));}catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict(new{message="A template with this name already exists."});}}
}