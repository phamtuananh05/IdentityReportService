namespace IdentityReportService.DTOs.Cards;

public class LibraryCardResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string CardNumber { get; set; } = string.Empty;

    public DateTime IssuedDate { get; set; }

    public DateTime ExpiredDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string EffectiveStatus { get; set; } = string.Empty;

    public bool IsExpired { get; set; }

    public int RemainingDays { get; set; }

    public bool CanBorrow { get; set; }

    public string? CannotBorrowReason { get; set; }

    public LibraryCardOwnerInfo? Owner { get; set; }
}

public class LibraryCardOwnerInfo
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string UserStatus { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
}