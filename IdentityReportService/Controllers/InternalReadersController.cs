using IdentityReportService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/internal/readers")]
public class InternalReadersController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly IConfiguration _configuration;

    public InternalReadersController(IdentityDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("{readerId:guid}/status")]
    public async Task<IActionResult> GetReaderStatus(
        Guid readerId,
        [FromHeader(Name = "X-Internal-Service-Key")] string? internalServiceKey)
    {
        if (!IsValidInternalKey(internalServiceKey))
        {
            return Unauthorized(new { message = "Internal service key không hợp lệ" });
        }

        var reader = await _context.Users
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == readerId && x.Role == "Reader");

        if (reader == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy độc giả",
                readerId
            });
        }

        var isUserLocked = reader.Status == "Locked";
        var isCardLocked = reader.LibraryCard != null && reader.LibraryCard.Status == "Locked";
        var isCardExpired = reader.LibraryCard != null && reader.LibraryCard.ExpiredDate < DateTime.UtcNow;

        return Ok(new
        {
            readerId = reader.Id,
            name = reader.FullName,
            email = reader.Email,
            isLocked = isUserLocked || isCardLocked,
            userStatus = reader.Status,
            cardStatus = reader.LibraryCard?.Status,
            cardExpiredDate = reader.LibraryCard?.ExpiredDate,
            isCardExpired
        });
    }

    private bool IsValidInternalKey(string? internalServiceKey)
    {
        var expectedKey = _configuration["InternalService:ApiKey"];

        return !string.IsNullOrWhiteSpace(expectedKey)
            && internalServiceKey == expectedKey;
    }
}