namespace IdentityReportService.DTOs.Auth;

public class AuthUserResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}