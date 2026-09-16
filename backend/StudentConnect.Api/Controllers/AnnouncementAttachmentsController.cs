using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/announcements")]
[Authorize]
public class AnnouncementAttachmentsController(
    AnnouncementStore announcements,
    IFileStorage fileStorage,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant) : ControllerBase
{
    [HttpGet("{id:int}/attachment")]
    public async Task<IActionResult> Download(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId) return Forbid();
        var attachment = await announcements.GetAttachmentAsync(id, schoolId, userId, currentUser.Role is "Teacher" or "Admin");
        if (attachment is null) return NotFound();
        var stream = await fileStorage.GetFileAsync(attachment.StoredFileName);
        return stream is null ? NotFound() : File(stream, attachment.ContentType, attachment.FileName);
    }
}