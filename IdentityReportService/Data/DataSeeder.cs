using IdentityReportService.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await context.Database.MigrateAsync();

        if (await context.Users.AnyAsync())
        {
            return;
        }

        var admin = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Quản trị hệ thống",
            Email = "admin@library.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = "Admin",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var librarian = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FullName = "Thủ thư thư viện",
            Email = "librarian@library.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = "Librarian",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var reader = new User
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FullName = "Nguyễn Văn A",
            Email = "reader@library.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = "Reader",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var readerProfile = new ReaderProfile
        {
            UserId = reader.Id,
            StudentCode = "SV001",
            Phone = "0987654321",
            Address = "Hà Nội",
            DateOfBirth = new DateTime(2004, 1, 1)
        };

        var libraryCard = new LibraryCard
        {
            UserId = reader.Id,
            CardNumber = "LIB-20260616-0001",
            IssuedDate = DateTime.UtcNow,
            ExpiredDate = DateTime.UtcNow.AddYears(1),
            Status = "Active"
        };

        context.Users.AddRange(admin, librarian, reader);
        context.ReaderProfiles.Add(readerProfile);
        context.LibraryCards.Add(libraryCard);

        await context.SaveChangesAsync();
    }
}