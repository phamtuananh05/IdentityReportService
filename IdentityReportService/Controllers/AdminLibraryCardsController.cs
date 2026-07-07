using IdentityReportService.Data;
using IdentityReportService.DTOs.Cards;
using IdentityReportService.Models;
using IdentityReportService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/cards")]
[Authorize(Roles = "Admin,Librarian")]
public class AdminLibraryCardsController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] ValidStatuses = { "Active", "Locked", "Expired" };

    public AdminLibraryCardsController(
        IdentityDbContext context,
        AuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCards(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            pageSize = 10;
        }

        var now = DateTime.UtcNow;

        var query = _context.LibraryCards
            .Include(x => x.User)
                .ThenInclude(x => x!.ReaderProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var search = keyword.Trim().ToLower();

            query = query.Where(x =>
                x.CardNumber.ToLower().Contains(search) ||
                (x.User != null && x.User.FullName.ToLower().Contains(search)) ||
                (x.User != null && x.User.Email.ToLower().Contains(search)) ||
                (x.User != null &&
                 x.User.ReaderProfile != null &&
                 x.User.ReaderProfile.StudentCode.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);

            if (!IsValidStatus(normalizedStatus))
            {
                return BadRequest(new
                {
                    message = "Trạng thái thẻ không hợp lệ. Chỉ chấp nhận Active, Locked hoặc Expired"
                });
            }

            if (normalizedStatus == "Expired")
            {
                query = query.Where(x =>
                    x.Status != "Locked" &&
                    x.ExpiredDate < now);
            }
            else if (normalizedStatus == "Active")
            {
                query = query.Where(x =>
                    x.Status == "Active" &&
                    x.ExpiredDate >= now);
            }
            else if (normalizedStatus == "Locked")
            {
                query = query.Where(x => x.Status == "Locked");
            }
        }

        var totalItems = await query.CountAsync();

        var cardEntities = await query
            .OrderByDescending(x => x.IssuedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var cards = cardEntities
            .Select(ToLibraryCardResponse)
            .ToList();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            items = cards
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCardById(Guid id)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
                .ThenInclude(x => x!.ReaderProfile)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        return Ok(ToLibraryCardResponse(card));
    }

    [HttpPatch("{id:guid}/lock")]
    public async Task<IActionResult> LockCard(Guid id)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        if (card.Status == "Locked")
        {
            return BadRequest(new { message = "Thẻ thư viện đã bị khóa trước đó" });
        }

        card.Status = "Locked";

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "LockLibraryCard",
            entityName: "LibraryCard",
            entityId: card.Id.ToString(),
            description: $"Khóa thẻ thư viện {card.CardNumber} của tài khoản {card.User?.Email}");

        return Ok(new
        {
            message = "Khóa thẻ thư viện thành công",
            cardId = card.Id,
            cardNumber = card.CardNumber,
            status = card.Status
        });
    }

    [HttpPatch("{id:guid}/unlock")]
    public async Task<IActionResult> UnlockCard(Guid id)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        if (card.Status == "Active" && card.ExpiredDate >= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Thẻ thư viện đang hoạt động" });
        }

        if (card.ExpiredDate < DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "Thẻ thư viện đã hết hạn. Vui lòng gia hạn thẻ trước khi mở khóa"
            });
        }

        card.Status = "Active";

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "UnlockLibraryCard",
            entityName: "LibraryCard",
            entityId: card.Id.ToString(),
            description: $"Mở khóa thẻ thư viện {card.CardNumber} của tài khoản {card.User?.Email}");

        return Ok(new
        {
            message = "Mở khóa thẻ thư viện thành công",
            cardId = card.Id,
            cardNumber = card.CardNumber,
            status = card.Status
        });
    }

    [HttpPatch("{id:guid}/extend")]
    public async Task<IActionResult> ExtendCard(
        Guid id,
        [FromBody] ExtendLibraryCardRequest request)
    {
        if (request.Months <= 0)
        {
            return BadRequest(new { message = "Số tháng gia hạn phải lớn hơn 0" });
        }

        if (request.Months > 60)
        {
            return BadRequest(new { message = "Không được gia hạn quá 60 tháng trong một lần" });
        }

        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        var oldExpiredDate = card.ExpiredDate;

        var baseDate = card.ExpiredDate > DateTime.UtcNow
            ? card.ExpiredDate
            : DateTime.UtcNow;

        card.ExpiredDate = baseDate.AddMonths(request.Months);
        card.Status = "Active";

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "ExtendLibraryCard",
            entityName: "LibraryCard",
            entityId: card.Id.ToString(),
            description: $"Gia hạn thẻ thư viện {card.CardNumber} thêm {request.Months} tháng. Hạn cũ: {oldExpiredDate:yyyy-MM-dd}, hạn mới: {card.ExpiredDate:yyyy-MM-dd}");

        return Ok(new
        {
            message = "Gia hạn thẻ thư viện thành công",
            cardId = card.Id,
            cardNumber = card.CardNumber,
            oldExpiredDate,
            newExpiredDate = card.ExpiredDate,
            status = card.Status
        });
    }

    private static bool IsValidStatus(string status)
    {
        return ValidStatuses.Contains(status);
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        var value = status.Trim().ToLower();

        return value switch
        {
            "active" => "Active",
            "locked" => "Locked",
            "expired" => "Expired",
            _ => status.Trim()
        };
    }

    private static LibraryCardResponse ToLibraryCardResponse(LibraryCard card)
    {
        var now = DateTime.UtcNow;

        var isExpired = card.ExpiredDate < now;
        var effectiveStatus = GetEffectiveStatus(card);
        var remainingDays = Math.Max(0, (int)Math.Ceiling((card.ExpiredDate - now).TotalDays));

        var canBorrow = true;
        string? cannotBorrowReason = null;

        if (card.User == null)
        {
            canBorrow = false;
            cannotBorrowReason = "Không tìm thấy tài khoản liên kết với thẻ";
        }
        else if (card.User.Status != "Active")
        {
            canBorrow = false;
            cannotBorrowReason = "Tài khoản người dùng đang bị khóa";
        }
        else if (card.Status == "Locked")
        {
            canBorrow = false;
            cannotBorrowReason = "Thẻ thư viện đang bị khóa";
        }
        else if (isExpired)
        {
            canBorrow = false;
            cannotBorrowReason = "Thẻ thư viện đã hết hạn";
        }

        return new LibraryCardResponse
        {
            Id = card.Id,
            UserId = card.UserId,
            CardNumber = card.CardNumber,
            IssuedDate = card.IssuedDate,
            ExpiredDate = card.ExpiredDate,
            Status = card.Status,
            EffectiveStatus = effectiveStatus,
            IsExpired = isExpired,
            RemainingDays = remainingDays,
            CanBorrow = canBorrow,
            CannotBorrowReason = cannotBorrowReason,
            Owner = card.User == null
                ? null
                : new LibraryCardOwnerInfo
                {
                    UserId = card.User.Id,
                    FullName = card.User.FullName,
                    Email = card.User.Email,
                    Role = card.User.Role,
                    UserStatus = card.User.Status,
                    StudentCode = card.User.ReaderProfile?.StudentCode ?? string.Empty,
                    Phone = card.User.ReaderProfile?.Phone ?? string.Empty
                }
        };
    }

    private static string GetEffectiveStatus(LibraryCard card)
    {
        if (card.Status == "Locked")
        {
            return "Locked";
        }

        if (card.ExpiredDate < DateTime.UtcNow)
        {
            return "Expired";
        }

        return card.Status;
    }
}