namespace IdentityReportService.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public AuthUserResponse User { get; set; } = new();
}