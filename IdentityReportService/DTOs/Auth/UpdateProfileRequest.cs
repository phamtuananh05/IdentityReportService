namespace IdentityReportService.DTOs.Auth;

public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateTime? DateOfBirth { get; set; }
}