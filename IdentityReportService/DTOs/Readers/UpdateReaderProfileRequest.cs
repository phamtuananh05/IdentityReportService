namespace IdentityReportService.DTOs.Readers;

public class UpdateReaderProfileRequest
{
    public string FullName { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }
}