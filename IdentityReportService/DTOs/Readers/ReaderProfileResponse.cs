namespace IdentityReportService.DTOs.Readers;

public class ReaderProfileResponse
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string UserStatus { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public string? CardNumber { get; set; }

    public DateTime? IssuedDate { get; set; }

    public DateTime? ExpiredDate { get; set; }

    public string? CardStatus { get; set; }
}