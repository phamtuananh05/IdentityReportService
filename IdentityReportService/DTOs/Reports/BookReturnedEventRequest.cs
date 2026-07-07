namespace IdentityReportService.DTOs.Reports;

public class BookReturnedEventRequest
{
    public Guid BorrowId { get; set; }

    public Guid BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public string? BookCategory { get; set; }

    public Guid ReaderId { get; set; }

    public string ReaderName { get; set; } = string.Empty;

    public DateTime? BorrowDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime ReturnDate { get; set; }

    public decimal FineAmount { get; set; }
}