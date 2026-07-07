namespace IdentityReportService.DTOs.Notifications;

public class NotificationResponse
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
}