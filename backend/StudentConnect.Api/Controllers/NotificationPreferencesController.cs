using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Lets any authenticated user (Parents, primarily) opt into a periodic email digest instead
// of a realtime email per notification — see NotificationDigestProcessor/EmailNotificationService.
[ApiController]
[Route("api/notification-preferences")]
[Authorize]
public class NotificationPreferencesController(UserStore users, ICurrentUserService currentUser) : ControllerBase
{
    // GET /api/notification-preferences
    [HttpGet]
    public async Task<ActionResult<NotificationPreferenceDto>> Get()
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await users.GetNotificationPreferencesAsync(userId));
    }

    // PUT /api/notification-preferences
    [HttpPut]
    public async Task<IActionResult> Update(NotificationPreferenceDto dto)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        await users.SetNotificationPreferencesAsync(userId, dto);
        return NoContent();
    }
}
