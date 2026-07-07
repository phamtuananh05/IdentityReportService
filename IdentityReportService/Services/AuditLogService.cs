using IdentityReportService.Data;
using IdentityReportService.Models;
using System.Security.Claims;

namespace IdentityReportService.Services;

public class AuditLogService
{
    private readonly IdentityDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        IdentityDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task WriteAsync(
        string action,
        string entityName,
        string? entityId,
        string description)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        var actorUserId = GetActorUserId(user);

        var actorEmail =
            user?.FindFirstValue(ClaimTypes.Email)
            ?? user?.FindFirstValue("email")
            ?? "Unknown";

        var actorRole =
            user?.FindFirstValue(ClaimTypes.Role)
            ?? user?.FindFirstValue("role")
            ?? "Unknown";

        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();

        var auditLog = new AuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync();
    }

    private static Guid? GetActorUserId(ClaimsPrincipal? user)
    {
        var userId =
            user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub")
            ?? user?.FindFirstValue("userId")
            ?? user?.FindFirstValue("id");

        if (Guid.TryParse(userId, out var id))
        {
            return id;
        }

        return null;
    }
}