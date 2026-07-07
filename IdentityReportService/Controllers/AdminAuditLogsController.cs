using IdentityReportService.Data;
using IdentityReportService.DTOs.AuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = "Admin")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public AdminAuditLogsController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? keyword,
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            pageSize = 10;
        }

        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var search = keyword.Trim().ToLower();

            query = query.Where(x =>
                x.ActorEmail.ToLower().Contains(search) ||
                x.Action.ToLower().Contains(search) ||
                x.EntityName.ToLower().Contains(search) ||
                x.Description.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(x => x.EntityName == entityName.Trim());
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toDate.Value);
        }

        var totalItems = await query.CountAsync();

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogResponse
            {
                Id = x.Id,
                ActorUserId = x.ActorUserId,
                ActorEmail = x.ActorEmail,
                ActorRole = x.ActorRole,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Description = x.Description,
                IpAddress = x.IpAddress,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            items = logs
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAuditLogById(Guid id)
    {
        var log = await _context.AuditLogs
            .Where(x => x.Id == id)
            .Select(x => new AuditLogResponse
            {
                Id = x.Id,
                ActorUserId = x.ActorUserId,
                ActorEmail = x.ActorEmail,
                ActorRole = x.ActorRole,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Description = x.Description,
                IpAddress = x.IpAddress,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (log == null)
        {
            return NotFound(new { message = "Không tìm thấy nhật ký hệ thống" });
        }

        return Ok(log);
    }
}