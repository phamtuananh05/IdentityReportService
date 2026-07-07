using IdentityReportService.Data;
using IdentityReportService.DTOs.LibraryCards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/library-cards")]
[Authorize]
public class LibraryCardsController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public LibraryCardsController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    [Authorize(Roles = "Reader")]
    [ProducesResponseType(typeof(LibraryCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCard()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        return await GetCardByUserId(userId.Value);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(LibraryCardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCard(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        var currentRole = User.FindFirstValue(ClaimTypes.Role);

        if (currentRole == "Reader" && currentUserId != userId)
        {
            return Forbid();
        }

        return await GetCardByUserId(userId);
    }

    [HttpPut("{userId:guid}/renew")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> RenewCard(Guid userId)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        var now = DateTime.UtcNow;

        if (card.ExpiredDate > now)
        {
            card.ExpiredDate = card.ExpiredDate.AddYears(1);
        }
        else
        {
            card.ExpiredDate = now.AddYears(1);
        }

        card.Status = "Active";

        if (card.User != null && card.User.Status != "Locked")
        {
            card.User.Status = "Active";
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Gia hạn thẻ thư viện thành công",
            userId = card.UserId,
            expiredDate = card.ExpiredDate,
            status = card.Status
        });
    }

    [HttpPut("{userId:guid}/lock")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> LockCard(Guid userId)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        card.Status = "Locked";

        if (card.User != null)
        {
            card.User.Status = "Locked";
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Khóa thẻ thư viện thành công",
            userId = card.UserId,
            status = card.Status
        });
    }

    [HttpPut("{userId:guid}/unlock")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> UnlockCard(Guid userId)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        card.Status = "Active";

        if (card.User != null)
        {
            card.User.Status = "Active";
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Mở khóa thẻ thư viện thành công",
            userId = card.UserId,
            status = card.Status
        });
    }

    private async Task<IActionResult> GetCardByUserId(Guid userId)
    {
        var card = await _context.LibraryCards
            .Include(x => x.User)
            .Where(x => x.UserId == userId)
            .Select(x => new LibraryCardResponse
            {
                Id = x.Id,
                UserId = x.UserId,
                FullName = x.User != null ? x.User.FullName : string.Empty,
                Email = x.User != null ? x.User.Email : string.Empty,
                CardNumber = x.CardNumber,
                IssuedDate = x.IssuedDate,
                ExpiredDate = x.ExpiredDate,
                Status = x.Status
            })
            .FirstOrDefaultAsync();

        if (card == null)
        {
            return NotFound(new { message = "Không tìm thấy thẻ thư viện" });
        }

        return Ok(card);
    }

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return Guid.Parse(userId);
    }
}