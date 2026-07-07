namespace IdentityReportService.Models;

public class ReaderProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public User? User { get; set; }
}