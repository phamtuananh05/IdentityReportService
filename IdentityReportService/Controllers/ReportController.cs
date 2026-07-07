using IdentityReportService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/report")]
[Authorize(Roles = "Admin,Librarian")]
public class ReportController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public ReportController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet("borrowReturn")]
    public async Task<IActionResult> GetBorrowReturnChart([FromQuery] int? year)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;

        var events = await _context.BorrowReportEvents
            .Where(x =>
                (x.EventType == "Borrowed" && x.BorrowDate.HasValue && x.BorrowDate.Value.Year == selectedYear)
                ||
                (x.EventType == "Returned" && x.ReturnDate.HasValue && x.ReturnDate.Value.Year == selectedYear))
            .Select(x => new
            {
                x.EventType,
                x.BorrowDate,
                x.ReturnDate,
                x.CreatedAt
            })
            .ToListAsync();

        var result = new List<object>();

        for (var month = 1; month <= 12; month++)
        {
            var borrowCount = events.Count(x =>
                x.EventType == "Borrowed"
                && (x.BorrowDate ?? x.CreatedAt).Year == selectedYear
                && (x.BorrowDate ?? x.CreatedAt).Month == month);

            var returnCount = events.Count(x =>
                x.EventType == "Returned"
                && (x.ReturnDate ?? x.CreatedAt).Year == selectedYear
                && (x.ReturnDate ?? x.CreatedAt).Month == month);

            result.Add(new
            {
                month = $"{selectedYear}-{month:00}",
                monthName = $"Tháng {month}",
                borrowCount,
                returnCount
            });
        }

        return Ok(result);
    }

    [HttpGet("categoryStats")]
    public async Task<IActionResult> GetCategoryStats([FromQuery] int? year)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;

        var events = await _context.BorrowReportEvents
            .Where(x =>
                x.EventType == "Borrowed"
                && x.BorrowDate.HasValue
                && x.BorrowDate.Value.Year == selectedYear)
            .Select(x => new
            {
                Category = string.IsNullOrWhiteSpace(x.BookCategory)
                    ? "Chưa phân loại"
                    : x.BookCategory
            })
            .ToListAsync();

        var total = events.Count;

        if (total == 0)
        {
            return Ok(new List<object>());
        }

        var result = events
            .GroupBy(x => x.Category)
            .Select(g => new
            {
                category = g.Key,
                borrowCount = g.Count(),
                percent = Math.Round((decimal)g.Count() / total * 100, 2)
            })
            .OrderByDescending(x => x.borrowCount)
            .ToList();

        return Ok(result);
    }

    [HttpGet("fineRevenue")]
    public async Task<IActionResult> GetFineRevenue([FromQuery] int? year)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;

        var events = await _context.BorrowReportEvents
            .Where(x =>
                x.EventType == "Returned"
                && x.ReturnDate.HasValue
                && x.ReturnDate.Value.Year == selectedYear)
            .Select(x => new
            {
                x.ReturnDate,
                x.CreatedAt,
                x.FineAmount
            })
            .ToListAsync();

        var result = new List<object>();

        for (var month = 1; month <= 12; month++)
        {
            var totalFine = events
                .Where(x =>
                    (x.ReturnDate ?? x.CreatedAt).Year == selectedYear
                    && (x.ReturnDate ?? x.CreatedAt).Month == month)
                .Sum(x => x.FineAmount);

            result.Add(new
            {
                month = $"{selectedYear}-{month:00}",
                monthName = $"Tháng {month}",
                totalFine
            });
        }

        return Ok(result);
    }
}