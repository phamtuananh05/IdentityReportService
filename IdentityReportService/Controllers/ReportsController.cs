using ClosedXML.Excel;
using IdentityReportService.Data;
using IdentityReportService.DTOs.Reports;
using IdentityReportService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly IConfiguration _configuration;

    public ReportsController(IdentityDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("events/book-borrowed")]
    public async Task<IActionResult> ReceiveBookBorrowedEvent(
        BookBorrowedEventRequest request,
        [FromHeader(Name = "X-Internal-Service-Key")] string? internalServiceKey)
    {
        var authResult = ValidateEventCaller(internalServiceKey);
        if (authResult != null) return authResult;

        if (request.BorrowId == Guid.Empty)
        {
            return BadRequest(new { message = "BorrowId không hợp lệ" });
        }

        if (request.BookId == Guid.Empty)
        {
            return BadRequest(new { message = "BookId không hợp lệ" });
        }

        if (request.ReaderId == Guid.Empty)
        {
            return BadRequest(new { message = "ReaderId không hợp lệ" });
        }

        if (string.IsNullOrWhiteSpace(request.BookTitle))
        {
            return BadRequest(new { message = "Tên sách không được để trống" });
        }

        if (string.IsNullOrWhiteSpace(request.ReaderName))
        {
            return BadRequest(new { message = "Tên độc giả không được để trống" });
        }

        var exists = await _context.BorrowReportEvents.AnyAsync(x =>
            x.BorrowId == request.BorrowId && x.EventType == "Borrowed");

        if (exists)
        {
            return Conflict(new { message = "Event mượn sách này đã tồn tại" });
        }

        var reportEvent = new BorrowReportEvent
        {
            BorrowId = request.BorrowId,
            BookId = request.BookId,
            BookTitle = request.BookTitle.Trim(),
            BookCategory = string.IsNullOrWhiteSpace(request.BookCategory)
        ? "Chưa phân loại"
        : request.BookCategory.Trim(),
            ReaderId = request.ReaderId,
            ReaderName = request.ReaderName.Trim(),
            EventType = "Borrowed",
            BorrowDate = request.BorrowDate,
            DueDate = request.DueDate,
            ReturnDate = null,
            FineAmount = 0,
            CreatedAt = request.BorrowDate
        };

        _context.BorrowReportEvents.Add(reportEvent);

        _context.Notifications.Add(new Notification
        {
            UserId = request.ReaderId,
            Type = "BorrowCreated",
            Message = $"Bạn đã mượn sách \"{request.BookTitle.Trim()}\" thành công. Hạn trả: {request.DueDate:dd/MM/yyyy}.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Report event saved successfully",
            eventType = reportEvent.EventType,
            borrowId = reportEvent.BorrowId
        });
    }

    [HttpPost("events/book-returned")]
    public async Task<IActionResult> ReceiveBookReturnedEvent(
        BookReturnedEventRequest request,
        [FromHeader(Name = "X-Internal-Service-Key")] string? internalServiceKey)
    {
        var authResult = ValidateEventCaller(internalServiceKey);
        if (authResult != null) return authResult;

        if (request.BorrowId == Guid.Empty)
        {
            return BadRequest(new { message = "BorrowId không hợp lệ" });
        }

        if (request.BookId == Guid.Empty)
        {
            return BadRequest(new { message = "BookId không hợp lệ" });
        }

        if (request.ReaderId == Guid.Empty)
        {
            return BadRequest(new { message = "ReaderId không hợp lệ" });
        }

        if (string.IsNullOrWhiteSpace(request.BookTitle))
        {
            return BadRequest(new { message = "Tên sách không được để trống" });
        }

        if (string.IsNullOrWhiteSpace(request.ReaderName))
        {
            return BadRequest(new { message = "Tên độc giả không được để trống" });
        }

        if (request.FineAmount < 0)
        {
            return BadRequest(new { message = "Tiền phạt không được âm" });
        }

        var exists = await _context.BorrowReportEvents.AnyAsync(x =>
            x.BorrowId == request.BorrowId && x.EventType == "Returned");

        if (exists)
        {
            return Conflict(new { message = "Event trả sách này đã tồn tại" });
        }

        var reportEvent = new BorrowReportEvent
        {
            BorrowId = request.BorrowId,
            BookId = request.BookId,
            BookTitle = request.BookTitle.Trim(),
            BookCategory = string.IsNullOrWhiteSpace(request.BookCategory)
        ? "Chưa phân loại"
        : request.BookCategory.Trim(),
            ReaderId = request.ReaderId,
            ReaderName = request.ReaderName.Trim(),
            EventType = "Returned",
            BorrowDate = request.BorrowDate,
            DueDate = request.DueDate,
            ReturnDate = request.ReturnDate,
            FineAmount = request.FineAmount,
            CreatedAt = request.ReturnDate
        };

        _context.BorrowReportEvents.Add(reportEvent);

        if (request.FineAmount > 0)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = request.ReaderId,
                Type = "FineCreated",
                Message = $"Bạn đã trả sách \"{request.BookTitle.Trim()}\" và phát sinh phí phạt {request.FineAmount:N0}đ.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }
        else
        {
            _context.Notifications.Add(new Notification
            {
                UserId = request.ReaderId,
                Type = "BookReturned",
                Message = $"Bạn đã trả sách \"{request.BookTitle.Trim()}\" thành công.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Report event saved successfully",
            eventType = reportEvent.EventType,
            borrowId = reportEvent.BorrowId
        });
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalReaders = await _context.Users.CountAsync(x => x.Role == "Reader");

        var totalBorrowed = await _context.BorrowReportEvents
            .CountAsync(x => x.EventType == "Borrowed");

        var totalReturned = await _context.BorrowReportEvents
            .CountAsync(x => x.EventType == "Returned");

        var borrowedIds = await _context.BorrowReportEvents
            .Where(x => x.EventType == "Borrowed")
            .Select(x => x.BorrowId)
            .Distinct()
            .ToListAsync();

        var returnedIds = await _context.BorrowReportEvents
            .Where(x => x.EventType == "Returned")
            .Select(x => x.BorrowId)
            .Distinct()
            .ToListAsync();

        var currentlyBorrowing = borrowedIds.Except(returnedIds).Count();

        var totalFineAmount = await _context.BorrowReportEvents
            .Where(x => x.EventType == "Returned")
            .SumAsync(x => (decimal?)x.FineAmount) ?? 0;

        var topBooks = await _context.BorrowReportEvents
            .Where(x => x.EventType == "Borrowed")
            .GroupBy(x => new { x.BookId, x.BookTitle })
            .Select(g => new
            {
                bookId = g.Key.BookId,
                bookTitle = g.Key.BookTitle,
                borrowCount = g.Count()
            })
            .OrderByDescending(x => x.borrowCount)
            .Take(5)
            .ToListAsync();

        return Ok(new
        {
            totalReaders,
            totalBorrowed,
            totalReturned,
            currentlyBorrowing,
            totalFineAmount,
            topBooks
        });
    }

    [HttpGet("top-books")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetTopBooks(
    [FromQuery] int? year,
    [FromQuery] int take = 10)
    {
        if (take <= 0) take = 10;
        if (take > 50) take = 50;

        var selectedYear = year ?? DateTime.UtcNow.Year;

        var result = await _context.BorrowReportEvents
            .Where(x =>
                x.EventType == "Borrowed" &&
                (
                    (x.BorrowDate.HasValue && x.BorrowDate.Value.Year == selectedYear)
                    ||
                    (!x.BorrowDate.HasValue && x.CreatedAt.Year == selectedYear)
                ))
            .GroupBy(x => new { x.BookId, x.BookTitle })
            .Select(g => new
            {
                bookId = g.Key.BookId,
                bookTitle = g.Key.BookTitle,
                borrowCount = g.Count()
            })
            .OrderByDescending(x => x.borrowCount)
            .Take(take)
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("top-readers")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetTopReaders(
    [FromQuery] int? year,
    [FromQuery] int take = 10)
    {
        if (take <= 0) take = 10;
        if (take > 50) take = 50;

        var selectedYear = year ?? DateTime.UtcNow.Year;

        var result = await _context.BorrowReportEvents
            .Where(x =>
                x.EventType == "Borrowed" &&
                (
                    (x.BorrowDate.HasValue && x.BorrowDate.Value.Year == selectedYear)
                    ||
                    (!x.BorrowDate.HasValue && x.CreatedAt.Year == selectedYear)
                ))
            .GroupBy(x => new { x.ReaderId, x.ReaderName })
            .Select(g => new
            {
                readerId = g.Key.ReaderId,
                readerName = g.Key.ReaderName,
                borrowCount = g.Count()
            })
            .OrderByDescending(x => x.borrowCount)
            .Take(take)
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("borrow-return")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetBorrowReturnStatistics(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var from = fromDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
        var to = toDate?.Date ?? DateTime.UtcNow.Date;

        if (from > to)
        {
            return BadRequest(new { message = "fromDate không được lớn hơn toDate" });
        }

        var toExclusive = to.AddDays(1);

        var events = await _context.BorrowReportEvents
            .Where(x => x.CreatedAt >= from && x.CreatedAt < toExclusive)
            .Select(x => new
            {
                x.EventType,
                Date = x.CreatedAt.Date
            })
            .ToListAsync();

        var result = new List<object>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var borrowCount = events.Count(x => x.Date == date && x.EventType == "Borrowed");
            var returnCount = events.Count(x => x.Date == date && x.EventType == "Returned");

            result.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                borrowCount,
                returnCount
            });
        }

        return Ok(result);
    }
    [HttpGet("new-users")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetNewUsersByMonth([FromQuery] int? year)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;

        var users = await _context.Users
            .Where(x => x.CreatedAt.Year == selectedYear)
            .Select(x => new
            {
                x.CreatedAt,
                x.Role
            })
            .ToListAsync();

        var result = new List<object>();

        for (var month = 1; month <= 12; month++)
        {
            var usersInMonth = users
                .Where(x => x.CreatedAt.Month == month)
                .ToList();

            result.Add(new
            {
                month = $"{selectedYear}-{month:00}",
                monthName = $"Tháng {month}",
                totalUsers = usersInMonth.Count,
                adminCount = usersInMonth.Count(x => x.Role == "Admin"),
                librarianCount = usersInMonth.Count(x => x.Role == "Librarian"),
                readerCount = usersInMonth.Count(x => x.Role == "Reader")
            });
        }

        return Ok(result);
    }
    [HttpGet("export/excel")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> ExportExcel([FromQuery] int? year)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;

        var users = await _context.Users
            .Where(x => x.CreatedAt.Year == selectedYear)
            .ToListAsync();

        var borrowEvents = await _context.BorrowReportEvents
            .Where(x =>
                x.EventType == "Borrowed" &&
                (
                    (x.BorrowDate.HasValue && x.BorrowDate.Value.Year == selectedYear)
                    ||
                    (!x.BorrowDate.HasValue && x.CreatedAt.Year == selectedYear)
                ))
            .ToListAsync();

        var returnEvents = await _context.BorrowReportEvents
            .Where(x =>
                x.EventType == "Returned" &&
                (
                    (x.ReturnDate.HasValue && x.ReturnDate.Value.Year == selectedYear)
                    ||
                    (!x.ReturnDate.HasValue && x.CreatedAt.Year == selectedYear)
                ))
            .ToListAsync();

        var allBorrowIds = borrowEvents
            .Select(x => x.BorrowId)
            .Distinct()
            .ToList();

        var allReturnedIds = returnEvents
            .Select(x => x.BorrowId)
            .Distinct()
            .ToList();

        var currentlyBorrowing = allBorrowIds
            .Except(allReturnedIds)
            .Count();

        var totalFineAmount = returnEvents.Sum(x => x.FineAmount);

        using var workbook = new XLWorkbook();

        // Sheet 1: Tổng quan
        var overviewSheet = workbook.Worksheets.Add("Tong quan");

        overviewSheet.Cell(1, 1).Value = "BÁO CÁO TỔNG QUAN THƯ VIỆN";
        overviewSheet.Range(1, 1, 1, 2).Merge();
        overviewSheet.Cell(1, 1).Style.Font.Bold = true;
        overviewSheet.Cell(1, 1).Style.Font.FontSize = 16;

        overviewSheet.Cell(3, 1).Value = "Năm";
        overviewSheet.Cell(3, 2).Value = selectedYear;

        overviewSheet.Cell(4, 1).Value = "Tổng tài khoản mới";
        overviewSheet.Cell(4, 2).Value = users.Count;

        overviewSheet.Cell(5, 1).Value = "Tổng độc giả mới";
        overviewSheet.Cell(5, 2).Value = users.Count(x => x.Role == "Reader");

        overviewSheet.Cell(6, 1).Value = "Tổng lượt mượn";
        overviewSheet.Cell(6, 2).Value = borrowEvents.Count;

        overviewSheet.Cell(7, 1).Value = "Tổng lượt trả";
        overviewSheet.Cell(7, 2).Value = returnEvents.Count;

        overviewSheet.Cell(8, 1).Value = "Số sách đang mượn";
        overviewSheet.Cell(8, 2).Value = currentlyBorrowing;

        overviewSheet.Cell(9, 1).Value = "Tổng phí phạt";
        overviewSheet.Cell(9, 2).Value = totalFineAmount;

        overviewSheet.Columns().AdjustToContents();

        // Sheet 2: Mượn trả theo tháng
        var borrowReturnSheet = workbook.Worksheets.Add("Muon tra theo thang");

        borrowReturnSheet.Cell(1, 1).Value = "Tháng";
        borrowReturnSheet.Cell(1, 2).Value = "Lượt mượn";
        borrowReturnSheet.Cell(1, 3).Value = "Lượt trả";
        borrowReturnSheet.Range(1, 1, 1, 3).Style.Font.Bold = true;

        for (var month = 1; month <= 12; month++)
        {
            var borrowCount = borrowEvents.Count(x =>
                (x.BorrowDate ?? x.CreatedAt).Month == month);

            var returnCount = returnEvents.Count(x =>
                (x.ReturnDate ?? x.CreatedAt).Month == month);

            var row = month + 1;

            borrowReturnSheet.Cell(row, 1).Value = $"Tháng {month}";
            borrowReturnSheet.Cell(row, 2).Value = borrowCount;
            borrowReturnSheet.Cell(row, 3).Value = returnCount;
        }

        borrowReturnSheet.Columns().AdjustToContents();

        // Sheet 3: Top sách
        var topBooksSheet = workbook.Worksheets.Add("Top sach");

        topBooksSheet.Cell(1, 1).Value = "STT";
        topBooksSheet.Cell(1, 2).Value = "BookId";
        topBooksSheet.Cell(1, 3).Value = "Tên sách";
        topBooksSheet.Cell(1, 4).Value = "Số lượt mượn";
        topBooksSheet.Range(1, 1, 1, 4).Style.Font.Bold = true;

        var topBooks = borrowEvents
            .GroupBy(x => new { x.BookId, x.BookTitle })
            .Select(g => new
            {
                g.Key.BookId,
                g.Key.BookTitle,
                BorrowCount = g.Count()
            })
            .OrderByDescending(x => x.BorrowCount)
            .Take(10)
            .ToList();

        for (var i = 0; i < topBooks.Count; i++)
        {
            var row = i + 2;

            topBooksSheet.Cell(row, 1).Value = i + 1;
            topBooksSheet.Cell(row, 2).Value = topBooks[i].BookId.ToString();
            topBooksSheet.Cell(row, 3).Value = topBooks[i].BookTitle;
            topBooksSheet.Cell(row, 4).Value = topBooks[i].BorrowCount;
        }

        topBooksSheet.Columns().AdjustToContents();

        // Sheet 4: Top độc giả
        var topReadersSheet = workbook.Worksheets.Add("Top doc gia");

        topReadersSheet.Cell(1, 1).Value = "STT";
        topReadersSheet.Cell(1, 2).Value = "ReaderId";
        topReadersSheet.Cell(1, 3).Value = "Tên độc giả";
        topReadersSheet.Cell(1, 4).Value = "Số lượt mượn";
        topReadersSheet.Range(1, 1, 1, 4).Style.Font.Bold = true;

        var topReaders = borrowEvents
            .GroupBy(x => new { x.ReaderId, x.ReaderName })
            .Select(g => new
            {
                g.Key.ReaderId,
                g.Key.ReaderName,
                BorrowCount = g.Count()
            })
            .OrderByDescending(x => x.BorrowCount)
            .Take(10)
            .ToList();

        for (var i = 0; i < topReaders.Count; i++)
        {
            var row = i + 2;

            topReadersSheet.Cell(row, 1).Value = i + 1;
            topReadersSheet.Cell(row, 2).Value = topReaders[i].ReaderId.ToString();
            topReadersSheet.Cell(row, 3).Value = topReaders[i].ReaderName;
            topReadersSheet.Cell(row, 4).Value = topReaders[i].BorrowCount;
        }

        topReadersSheet.Columns().AdjustToContents();

        // Sheet 5: Doanh thu phí phạt
        var fineSheet = workbook.Worksheets.Add("Phi phat");

        fineSheet.Cell(1, 1).Value = "Tháng";
        fineSheet.Cell(1, 2).Value = "Tổng phí phạt";
        fineSheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

        for (var month = 1; month <= 12; month++)
        {
            var totalFine = returnEvents
                .Where(x => (x.ReturnDate ?? x.CreatedAt).Month == month)
                .Sum(x => x.FineAmount);

            var row = month + 1;

            fineSheet.Cell(row, 1).Value = $"Tháng {month}";
            fineSheet.Cell(row, 2).Value = totalFine;
        }

        fineSheet.Columns().AdjustToContents();

        // Sheet 6: User mới
        var newUsersSheet = workbook.Worksheets.Add("User moi");

        newUsersSheet.Cell(1, 1).Value = "Tháng";
        newUsersSheet.Cell(1, 2).Value = "Tổng user mới";
        newUsersSheet.Cell(1, 3).Value = "Admin";
        newUsersSheet.Cell(1, 4).Value = "Librarian";
        newUsersSheet.Cell(1, 5).Value = "Reader";
        newUsersSheet.Range(1, 1, 1, 5).Style.Font.Bold = true;

        for (var month = 1; month <= 12; month++)
        {
            var usersInMonth = users
                .Where(x => x.CreatedAt.Month == month)
                .ToList();

            var row = month + 1;

            newUsersSheet.Cell(row, 1).Value = $"Tháng {month}";
            newUsersSheet.Cell(row, 2).Value = usersInMonth.Count;
            newUsersSheet.Cell(row, 3).Value = usersInMonth.Count(x => x.Role == "Admin");
            newUsersSheet.Cell(row, 4).Value = usersInMonth.Count(x => x.Role == "Librarian");
            newUsersSheet.Cell(row, 5).Value = usersInMonth.Count(x => x.Role == "Reader");
        }

        newUsersSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"library-report-{selectedYear}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private IActionResult? ValidateEventCaller(string? internalServiceKey)
    {
        if (IsValidInternalKey(internalServiceKey))
        {
            return null;
        }

        if (User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Admin") || User.IsInRole("Librarian")))
        {
            return null;
        }

        return Unauthorized(new { message = "Thiếu hoặc sai Internal Service Key" });
    }

    private bool IsValidInternalKey(string? internalServiceKey)
    {
        var expectedKey = _configuration["InternalService:ApiKey"];

        return !string.IsNullOrWhiteSpace(expectedKey)
            && internalServiceKey == expectedKey;
    }
}