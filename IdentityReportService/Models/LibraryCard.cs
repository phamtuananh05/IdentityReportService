namespace IdentityReportService.Models;

public class LibraryCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string CardNumber { get; set; } = string.Empty;

    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    public DateTime ExpiredDate { get; set; } = DateTime.UtcNow.AddYears(1);

    public string Status { get; set; } = "Active";
    // Active, Expired, Locked

    public User? User { get; set; }
}