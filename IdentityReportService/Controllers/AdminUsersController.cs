using IdentityReportService.Data;
using IdentityReportService.DTOs.Admin;
using IdentityReportService.Models;
using IdentityReportService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] ValidRoles = { "Admin", "Librarian", "Reader" };

    public AdminUsersController(
        IdentityDbContext context,
        AuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
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

        var query = _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var search = keyword.Trim().ToLower();

            query = query.Where(x =>
                x.FullName.ToLower().Contains(search) ||
                x.Email.ToLower().Contains(search) ||
                (x.ReaderProfile != null &&
                 x.ReaderProfile.StudentCode.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = NormalizeRole(role);

            if (!IsValidRole(normalizedRole))
            {
                return BadRequest(new
                {
                    message = "Vai trò lọc không hợp lệ. Chỉ chấp nhận Admin, Librarian hoặc Reader"
                });
            }

            query = query.Where(x => x.Role == normalizedRole);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);

            if (!IsValidStatus(normalizedStatus))
            {
                return BadRequest(new
                {
                    message = "Trạng thái lọc không hợp lệ. Chỉ chấp nhận Active hoặc Locked"
                });
            }

            query = query.Where(x => x.Status == normalizedStatus);
        }

        var totalItems = await query.CountAsync();

        var userEntities = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var users = userEntities
            .Select(ToAdminUserResponse)
            .ToList();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            items = users
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        return Ok(ToAdminUserResponse(user));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "Họ tên không được để trống" });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email không được để trống" });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });
        }

        var role = NormalizeRole(request.Role);

        if (!IsValidRole(role))
        {
            return BadRequest(new
            {
                message = "Vai trò không hợp lệ. Chỉ chấp nhận Admin, Librarian hoặc Reader"
            });
        }

        var email = request.Email.Trim().ToLower();

        var emailExists = await _context.Users.AnyAsync(x => x.Email == email);

        if (emailExists)
        {
            return Conflict(new { message = "Email đã tồn tại" });
        }

        if (role == "Reader" && string.IsNullOrWhiteSpace(request.StudentCode))
        {
            return BadRequest(new
            {
                message = "Mã sinh viên không được để trống khi tạo tài khoản Reader"
            });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        if (role == "Reader")
        {
            var profile = new ReaderProfile
            {
                UserId = user.Id,
                StudentCode = request.StudentCode?.Trim() ?? string.Empty,
                Phone = request.Phone?.Trim() ?? string.Empty,
                Address = request.Address?.Trim() ?? string.Empty,
                DateOfBirth = request.DateOfBirth
            };

            var card = new LibraryCard
            {
                UserId = user.Id,
                CardNumber = await GenerateLibraryCardNumberAsync(),
                IssuedDate = DateTime.UtcNow,
                ExpiredDate = DateTime.UtcNow.AddYears(1),
                Status = "Active"
            };

            _context.ReaderProfiles.Add(profile);
            _context.LibraryCards.Add(card);
        }

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "CreateUser",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Admin tạo tài khoản {user.Email} với vai trò {user.Role}");

        var createdUser = await _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .FirstAsync(x => x.Id == user.Id);

        return Ok(new
        {
            message = "Tạo tài khoản thành công",
            user = ToAdminUserResponse(createdUser)
        });
    }

    [HttpPatch("{id:guid}/lock")]
    public async Task<IActionResult> LockUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(new
            {
                message = "Admin không được tự khóa tài khoản của chính mình"
            });
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        if (user.Status == "Locked")
        {
            return BadRequest(new { message = "Tài khoản đã bị khóa trước đó" });
        }

        user.Status = "Locked";

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "LockUser",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Admin khóa tài khoản {user.Email}");

        return Ok(new
        {
            message = "Khóa tài khoản thành công",
            userId = user.Id,
            status = user.Status
        });
    }

    [HttpPatch("{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        if (user.Status == "Active")
        {
            return BadRequest(new { message = "Tài khoản đang hoạt động" });
        }

        user.Status = "Active";

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "UnlockUser",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Admin mở khóa tài khoản {user.Email}");

        return Ok(new
        {
            message = "Mở khóa tài khoản thành công",
            userId = user.Id,
            status = user.Status
        });
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(
        Guid id,
        [FromBody] UpdateUserRoleRequest request)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(new
            {
                message = "Admin không được tự thay đổi vai trò của chính mình"
            });
        }

        var newRole = NormalizeRole(request.Role);

        if (!IsValidRole(newRole))
        {
            return BadRequest(new
            {
                message = "Vai trò không hợp lệ. Chỉ chấp nhận Admin, Librarian hoặc Reader"
            });
        }

        var user = await _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        if (user.Role == newRole)
        {
            return BadRequest(new { message = "Tài khoản đã có vai trò này" });
        }

        var oldRole = user.Role;

        user.Role = newRole;

        if (newRole == "Reader")
        {
            if (user.ReaderProfile == null)
            {
                _context.ReaderProfiles.Add(new ReaderProfile
                {
                    UserId = user.Id,
                    StudentCode = $"R-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Phone = string.Empty,
                    Address = string.Empty
                });
            }

            if (user.LibraryCard == null)
            {
                _context.LibraryCards.Add(new LibraryCard
                {
                    UserId = user.Id,
                    CardNumber = await GenerateLibraryCardNumberAsync(),
                    IssuedDate = DateTime.UtcNow,
                    ExpiredDate = DateTime.UtcNow.AddYears(1),
                    Status = "Active"
                });
            }
        }

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "UpdateUserRole",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Admin đổi vai trò tài khoản {user.Email} từ {oldRole} sang {newRole}");

        return Ok(new
        {
            message = "Cập nhật vai trò thành công",
            userId = user.Id,
            oldRole,
            newRole
        });
    }

    [HttpPatch("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 6)
        {
            return BadRequest(new
            {
                message = "Mật khẩu mới phải có ít nhất 6 ký tự"
            });
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "ResetUserPassword",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Admin reset mật khẩu cho tài khoản {user.Email}");

        return Ok(new
        {
            message = "Reset mật khẩu thành công",
            userId = user.Id
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("id");

        if (Guid.TryParse(userId, out var id))
        {
            return id;
        }

        return null;
    }

    private static bool IsValidRole(string role)
    {
        return ValidRoles.Contains(role);
    }

    private static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return string.Empty;
        }

        var value = role.Trim().ToLower();

        return value switch
        {
            "admin" => "Admin",
            "librarian" => "Librarian",
            "reader" => "Reader",
            _ => role.Trim()
        };
    }

    private static bool IsValidStatus(string status)
    {
        return status == "Active" || status == "Locked";
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
            _ => status.Trim()
        };
    }

    private async Task<string> GenerateLibraryCardNumberAsync()
    {
        var prefix = $"LIB-{DateTime.UtcNow:yyyyMMdd}";

        for (var i = 0; i < 10; i++)
        {
            var cardNumber = $"{prefix}-{Random.Shared.Next(1000, 9999)}";

            var exists = await _context.LibraryCards
                .AnyAsync(x => x.CardNumber == cardNumber);

            if (!exists)
            {
                return cardNumber;
            }
        }

        return $"{prefix}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
    }

    private static AdminUserResponse ToAdminUserResponse(User user)
    {
        return new AdminUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            ReaderProfile = user.ReaderProfile == null
                ? null
                : new ReaderProfileInfo
                {
                    Id = user.ReaderProfile.Id,
                    StudentCode = user.ReaderProfile.StudentCode,
                    Phone = user.ReaderProfile.Phone,
                    Address = user.ReaderProfile.Address,
                    DateOfBirth = user.ReaderProfile.DateOfBirth
                },
            LibraryCard = user.LibraryCard == null
                ? null
                : new LibraryCardInfo
                {
                    Id = user.LibraryCard.Id,
                    CardNumber = user.LibraryCard.CardNumber,
                    IssuedDate = user.LibraryCard.IssuedDate,
                    ExpiredDate = user.LibraryCard.ExpiredDate,
                    Status = user.LibraryCard.Status
                }
        };
    }
}