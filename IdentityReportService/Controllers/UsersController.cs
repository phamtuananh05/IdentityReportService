using IdentityReportService.Data;
using IdentityReportService.DTOs.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IdentityDbContext _context;

    public UsersController(IdentityDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? keyword)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(x => x.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim().ToLower();

            query = query.Where(x =>
                x.FullName.ToLower().Contains(keyword) ||
                x.Email.ToLower().Contains(keyword));
        }

        var users = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new UserResponse
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _context.Users
            .Where(x => x.Id == id)
            .Select(x => new UserResponse
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        return Ok(user);
    }

    [HttpPut("{id:guid}/lock")]
    public async Task<IActionResult> LockUser(Guid id)
    {
        var user = await _context.Users
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        if (user.Role == "Admin")
        {
            return BadRequest(new { message = "Không được khóa tài khoản Admin" });
        }

        user.Status = "Locked";

        if (user.LibraryCard != null)
        {
            user.LibraryCard.Status = "Locked";
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Khóa tài khoản thành công",
            userId = user.Id,
            status = user.Status
        });
    }

    [HttpPut("{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id)
    {
        var user = await _context.Users
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        user.Status = "Active";

        if (user.LibraryCard != null)
        {
            user.LibraryCard.Status = "Active";
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Mở khóa tài khoản thành công",
            userId = user.Id,
            status = user.Status
        });
    }
}