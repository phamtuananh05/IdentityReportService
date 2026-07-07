namespace IdentityReportService.DTOs.Admin;

public class CreateAdminUserRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "Reader";
    // Admin, Librarian, Reader

    public string? StudentCode { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateTime? DateOfBirth { get; set; }
}