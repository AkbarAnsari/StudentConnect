using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Every action here is implicitly scoped to the caller — a user only ever sees their own
// notifications, there is no "get notifications for user X" endpoint at all.
[ApiController]
[Route("api/notifications")]
public class NotificationsController(StudentConnectDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // GET /api/notifications
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationResponseDto>>> GetMyNotifications()
    {
        var userId = currentUser.UserId!.Value;
        var notifications = await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .Select(n => NotificationResponseDto.FromEntity(n))
            .ToListAsync();

        return Ok(notifications);
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var userId = currentUser.UserId!.Value;
        var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(count);
    }

    // POST /api/notifications/{id}/read
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = currentUser.UserId!.Value;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null)
        {
            return NotFound();
        }

        notification.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/notifications/read-all
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = currentUser.UserId!.Value;
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));

        return NoContent();
    }
}
