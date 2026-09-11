using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Every action here is implicitly scoped to the caller — a user only ever sees their own
// notifications, there is no "get notifications for user X" endpoint at all.
[ApiController]
[Route("api/notifications")]
public class NotificationsController(NotificationStore notificationStore, ICurrentUserService currentUser) : ControllerBase
{
    // GET /api/notifications
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationResponseDto>>> GetMyNotifications()
    {
        var userId = currentUser.UserId!.Value;
        var notifications = await notificationStore.GetForUserAsync(userId);
        return Ok(notifications.Select(NotificationResponseDto.FromEntity));
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var userId = currentUser.UserId!.Value;
        var count = await notificationStore.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    // POST /api/notifications/{id}/read
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = currentUser.UserId!.Value;
        var updated = await notificationStore.MarkAsReadAsync(id, userId);
        if (updated == 0)
        {
            return NotFound();
        }

        return NoContent();
    }

    // POST /api/notifications/read-all
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = currentUser.UserId!.Value;
        await notificationStore.MarkAllAsReadAsync(userId);
        return NoContent();
    }
}
