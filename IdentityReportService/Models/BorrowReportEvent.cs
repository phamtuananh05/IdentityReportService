namespace IdentityReportService.Models;

public class BorrowReportEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BorrowId { get; set; }

    public Guid BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public string BookCategory { get; set; } = "Chưa phân loại";

    public Guid ReaderId { get; set; }

    public string ReaderName { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;
    // Borrowed, Returned

    public DateTime? BorrowDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public decimal FineAmount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}