using IdentityReportService.DTOs;

namespace IdentityReportService.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Reader";
    // Admin, Librarian, Reader

    public string Status { get; set; } = "Active";
    // Active, Locked

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ReaderProfile? ReaderProfile { get; set; }

    public LibraryCard? LibraryCard { get; set; }
}