namespace IdentityReportService.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ActorUserId { get; set; }

    public string ActorEmail { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}