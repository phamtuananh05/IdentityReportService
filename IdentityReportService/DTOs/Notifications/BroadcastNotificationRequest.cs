namespace IdentityReportService.DTOs.Notifications;

public class BroadcastNotificationRequest
{
    public string Message { get; set; } = string.Empty;

    public string TargetRole { get; set; } = "All";
    // All, Admin, Librarian, Reader
}