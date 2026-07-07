using System.Security.Claims;
using IdentityReportService.Data;
using IdentityReportService.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public NotificationsController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] string? type)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được người dùng từ token" });
        }

        var query = _context.Notifications
            .Where(x => x.UserId == userId.Value)
            .AsQueryable();

        if (isRead.HasValue)
        {
            query = query.Where(x => x.IsRead == isRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.Type == type.Trim());
        }

        var notifications = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new NotificationResponse
            {
                Id = x.Id,
                Type = x.Type,
                Message = x.Message,
                CreatedAt = x.CreatedAt,
                IsRead = x.IsRead
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được người dùng từ token" });
        }

        var unreadCount = await _context.Notifications
            .CountAsync(x => x.UserId == userId.Value && !x.IsRead);

        return Ok(new
        {
            unreadCount
        });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được người dùng từ token" });
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);

        if (notification == null)
        {
            return NotFound(new { message = "Không tìm thấy thông báo" });
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message = "Đã đánh dấu thông báo là đã đọc",
            id = notification.Id
        });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được người dùng từ token" });
        }

        var notifications = await _context.Notifications
            .Where(x => x.UserId == userId.Value && !x.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã đánh dấu tất cả thông báo là đã đọc",
            count = notifications.Count
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được người dùng từ token" });
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value);

        if (notification == null)
        {
            return NotFound(new { message = "Không tìm thấy thông báo" });
        }

        _context.Notifications.Remove(notification);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa thông báo thành công",
            id
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("id");

        if (Guid.TryParse(userIdValue, out var userId))
        {
            return userId;
        }

        return null;
    }
}