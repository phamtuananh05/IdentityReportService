using IdentityReportService.Data;
using IdentityReportService.DTOs.Readers;
using IdentityReportService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/readers")]
[Authorize]
public class ReadersController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public ReadersController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Librarian")]
    [ProducesResponseType(typeof(List<ReaderProfileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReaders([FromQuery] string? keyword)
    {
        var query = _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .Where(x => x.Role == "Reader")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim().ToLower();

            query = query.Where(x =>
                x.FullName.ToLower().Contains(keyword) ||
                x.Email.ToLower().Contains(keyword) ||
                (x.ReaderProfile != null && x.ReaderProfile.StudentCode.ToLower().Contains(keyword)));
        }

        var readers = await query
            .OrderBy(x => x.FullName)
            .Select(x => new ReaderProfileResponse
            {
                UserId = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role,
                UserStatus = x.Status,
                StudentCode = x.ReaderProfile != null ? x.ReaderProfile.StudentCode : string.Empty,
                Phone = x.ReaderProfile != null ? x.ReaderProfile.Phone : string.Empty,
                Address = x.ReaderProfile != null ? x.ReaderProfile.Address : string.Empty,
                DateOfBirth = x.ReaderProfile != null ? x.ReaderProfile.DateOfBirth : null,
                CardNumber = x.LibraryCard != null ? x.LibraryCard.CardNumber : null,
                IssuedDate = x.LibraryCard != null ? x.LibraryCard.IssuedDate : null,
                ExpiredDate = x.LibraryCard != null ? x.LibraryCard.ExpiredDate : null,
                CardStatus = x.LibraryCard != null ? x.LibraryCard.Status : null
            })
            .ToListAsync();

        return Ok(readers);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Reader")]
    [ProducesResponseType(typeof(ReaderProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        return await GetReaderProfileByUserId(userId.Value);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ReaderProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReaderById(Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        var currentRole = User.FindFirstValue(ClaimTypes.Role);

        if (currentRole == "Reader" && currentUserId != userId)
        {
            return Forbid();
        }

        return await GetReaderProfileByUserId(userId);
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateReader(Guid userId, UpdateReaderProfileRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var currentRole = User.FindFirstValue(ClaimTypes.Role);

        if (currentRole == "Reader" && currentUserId != userId)
        {
            return Forbid();
        }

        var user = await _context.Users
            .Include(x => x.ReaderProfile)
            .FirstOrDefaultAsync(x => x.Id == userId && x.Role == "Reader");

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy độc giả" });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "Họ tên không được để trống" });
        }

        user.FullName = request.FullName.Trim();

        if (user.ReaderProfile == null)
        {
            user.ReaderProfile = new ReaderProfile
            {
                UserId = user.Id
            };
        }

        user.ReaderProfile.StudentCode = request.StudentCode.Trim();
        user.ReaderProfile.Phone = request.Phone.Trim();
        user.ReaderProfile.Address = request.Address.Trim();
        user.ReaderProfile.DateOfBirth = request.DateOfBirth;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật hồ sơ độc giả thành công",
            userId = user.Id
        });
    }

    private async Task<IActionResult> GetReaderProfileByUserId(Guid userId)
    {
        var reader = await _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .Where(x => x.Id == userId && x.Role == "Reader")
            .Select(x => new ReaderProfileResponse
            {
                UserId = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role,
                UserStatus = x.Status,
                StudentCode = x.ReaderProfile != null ? x.ReaderProfile.StudentCode : string.Empty,
                Phone = x.ReaderProfile != null ? x.ReaderProfile.Phone : string.Empty,
                Address = x.ReaderProfile != null ? x.ReaderProfile.Address : string.Empty,
                DateOfBirth = x.ReaderProfile != null ? x.ReaderProfile.DateOfBirth : null,
                CardNumber = x.LibraryCard != null ? x.LibraryCard.CardNumber : null,
                IssuedDate = x.LibraryCard != null ? x.LibraryCard.IssuedDate : null,
                ExpiredDate = x.LibraryCard != null ? x.LibraryCard.ExpiredDate : null,
                CardStatus = x.LibraryCard != null ? x.LibraryCard.Status : null
            })
            .FirstOrDefaultAsync();

        if (reader == null)
        {
            return NotFound(new { message = "Không tìm thấy độc giả" });
        }

        return Ok(reader);
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