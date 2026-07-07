using IdentityReportService.Data;
using IdentityReportService.DTOs.Notifications;
using IdentityReportService.Models;
using IdentityReportService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "Admin")]
public class AdminNotificationsController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] ValidTargetRoles =
    {
        "All",
        "Admin",
        "Librarian",
        "Reader"
    };

    public AdminNotificationsController(
        IdentityDbContext context,
        AuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> BroadcastNotification(
        [FromBody] BroadcastNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Nội dung thông báo không được để trống" });
        }

        var targetRole = NormalizeTargetRole(request.TargetRole);

        if (!ValidTargetRoles.Contains(targetRole))
        {
            return BadRequest(new
            {
                message = "TargetRole không hợp lệ. Chỉ chấp nhận All, Admin, Librarian hoặc Reader"
            });
        }

        var usersQuery = _context.Users
            .Where(x => x.Status == "Active")
            .AsQueryable();

        if (targetRole != "All")
        {
            usersQuery = usersQuery.Where(x => x.Role == targetRole);
        }

        var users = await usersQuery.ToListAsync();

        if (users.Count == 0)
        {
            return BadRequest(new { message = "Không có người dùng phù hợp để gửi thông báo" });
        }

        var notifications = users.Select(user => new Notification
        {
            UserId = user.Id,
            Type = "System",
            Message = request.Message.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        }).ToList();

        _context.Notifications.AddRange(notifications);

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "BroadcastNotification",
            entityName: "Notification",
            entityId: null,
            description: $"Admin gửi thông báo hệ thống đến nhóm {targetRole}. Số người nhận: {users.Count}");

        return Ok(new
        {
            message = "Gửi thông báo hệ thống thành công",
            targetRole,
            receiverCount = users.Count
        });
    }

    private static string NormalizeTargetRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "All";
        }

        var value = role.Trim().ToLower();

        return value switch
        {
            "all" => "All",
            "admin" => "Admin",
            "librarian" => "Librarian",
            "reader" => "Reader",
            _ => role.Trim()
        };
    }
}