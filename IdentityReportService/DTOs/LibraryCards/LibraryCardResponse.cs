namespace IdentityReportService.DTOs.LibraryCards;

public class LibraryCardResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public DateTime IssuedDate { get; set; }

    public DateTime ExpiredDate { get; set; }

    public string Status { get; set; } = string.Empty;
}