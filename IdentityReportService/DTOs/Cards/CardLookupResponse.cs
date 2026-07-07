namespace IdentityReportService.DTOs.Cards;

public class CardLookupResponse
{
    public Guid UserId { get; set; }

    public Guid? ReaderProfileId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? StudentCode { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string CardNumber { get; set; } = string.Empty;

    public string CardStatus { get; set; } = string.Empty;

    public DateTime ExpiredDate { get; set; }

    public bool CanBorrow { get; set; }

    public string Reason { get; set; } = string.Empty;
}