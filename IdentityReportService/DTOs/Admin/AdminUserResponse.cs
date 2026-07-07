namespace IdentityReportService.DTOs.Admin;

public class AdminUserResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ReaderProfileInfo? ReaderProfile { get; set; }

    public LibraryCardInfo? LibraryCard { get; set; }
}

public class ReaderProfileInfo
{
    public Guid Id { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }
}

public class LibraryCardInfo
{
    public Guid Id { get; set; }

    public string CardNumber { get; set; } = string.Empty;

    public DateTime IssuedDate { get; set; }

    public DateTime ExpiredDate { get; set; }

    public string Status { get; set; } = string.Empty;
}