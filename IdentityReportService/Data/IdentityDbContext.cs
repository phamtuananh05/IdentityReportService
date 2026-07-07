using IdentityReportService.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ReaderProfile> ReaderProfiles => Set<ReaderProfile>();
    public DbSet<LibraryCard> LibraryCards => Set<LibraryCard>();
    public DbSet<BorrowReportEvent> BorrowReportEvents => Set<BorrowReportEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.Property(x => x.Role)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<ReaderProfile>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.StudentCode)
                .HasMaxLength(50);

            entity.Property(x => x.Phone)
                .HasMaxLength(20);

            entity.Property(x => x.Address)
                .HasMaxLength(255);

            entity.HasOne(x => x.User)
                .WithOne(x => x.ReaderProfile)
                .HasForeignKey<ReaderProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LibraryCard>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CardNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.CardNumber)
                .IsUnique();

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(x => x.User)
                .WithOne(x => x.LibraryCard)
                .HasForeignKey<LibraryCard>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BorrowReportEvent>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.BookTitle)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.BookCategory)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("Chưa phân loại");

            entity.Property(x => x.ReaderName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.EventType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.FineAmount)
                .HasPrecision(18, 2);
        });
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.IsRead)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.UserId);

            entity.HasIndex(x => new { x.UserId, x.IsRead });
        });
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ActorEmail)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.ActorRole)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Action)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.EntityName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.EntityId)
                .HasMaxLength(100);

            entity.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(x => x.IpAddress)
                .HasMaxLength(100);

            entity.HasIndex(x => x.ActorUserId);

            entity.HasIndex(x => x.Action);

            entity.HasIndex(x => x.EntityName);

            entity.HasIndex(x => x.CreatedAt);
        });
    }
}