using IdentityReportService.Data;
using IdentityReportService.DTOs.Cards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/card")]
public class CardController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly IConfiguration _configuration;

    public CardController(IdentityDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("{cardNumber}")]
    public async Task<IActionResult> GetReaderByCardNumber(
        string cardNumber,
        [FromHeader(Name = "X-Internal-Service-Key")] string? internalServiceKey)
    {
        var authResult = ValidateCaller(internalServiceKey);
        if (authResult != null) return authResult;

        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return BadRequest(new { message = "Mã thẻ không được để trống" });
        }

        var normalizedCardNumber = cardNumber.Trim();

        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.CardNumber == normalizedCardNumber);

        if (card == null)
        {
            return NotFound(new
            {
                canBorrow = false,
                reason = "Không tìm thấy thẻ thư viện"
            });
        }

        if (card.User == null)
        {
            return NotFound(new
            {
                canBorrow = false,
                reason = "Không tìm thấy tài khoản gắn với thẻ thư viện"
            });
        }

        var readerProfile = await _context.ReaderProfiles
            .FirstOrDefaultAsync(x => x.UserId == card.UserId);

        var canBorrow = true;
        var reason = "Thẻ hợp lệ";

        if (card.User.Role != "Reader")
        {
            canBorrow = false;
            reason = "Tài khoản này không phải độc giả";
        }
        else if (card.User.Status != "Active")
        {
            canBorrow = false;
            reason = "Tài khoản độc giả đang bị khóa hoặc không hoạt động";
        }
        else if (card.Status != "Active")
        {
            canBorrow = false;
            reason = "Thẻ thư viện đang bị khóa hoặc không hoạt động";
        }
        else if (card.ExpiredDate.Date < DateTime.UtcNow.Date)
        {
            canBorrow = false;
            reason = "Thẻ thư viện đã hết hạn";
        }

        var response = new CardLookupResponse
        {
            UserId = card.User.Id,
            ReaderProfileId = readerProfile?.Id,
            FullName = card.User.FullName,
            Email = card.User.Email,
            StudentCode = readerProfile?.StudentCode,
            Phone = readerProfile?.Phone,
            Address = readerProfile?.Address,
            CardNumber = card.CardNumber,
            CardStatus = card.Status,
            ExpiredDate = card.ExpiredDate,
            CanBorrow = canBorrow,
            Reason = reason
        };

        return Ok(response);
    }

    private IActionResult? ValidateCaller(string? internalServiceKey)
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

        return Unauthorized(new { message = "Bạn không có quyền tra cứu thẻ thư viện" });
    }

    private bool IsValidInternalKey(string? internalServiceKey)
    {
        var expectedKey = _configuration["InternalService:ApiKey"];

        return !string.IsNullOrWhiteSpace(expectedKey)
            && internalServiceKey == expectedKey;
    }
}