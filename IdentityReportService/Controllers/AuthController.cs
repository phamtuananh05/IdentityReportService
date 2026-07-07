using Google.Apis.Auth;
using IdentityReportService.Data;
using IdentityReportService.DTOs.Auth;
using IdentityReportService.Models;
using IdentityReportService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityReportService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly AuditLogService _auditLogService;

    public AuthController(
    IdentityDbContext context,
    JwtService jwtService,
    IConfiguration configuration,
    AuditLogService auditLogService)
    {
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
        _auditLogService = auditLogService;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
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

        var email = request.Email.Trim().ToLower();

        var emailExists = await _context.Users.AnyAsync(x => x.Email == email);

        if (emailExists)
        {
            return Conflict(new { message = "Email đã tồn tại" });
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Reader",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var profile = new ReaderProfile
        {
            UserId = user.Id,
            StudentCode = request.StudentCode.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            DateOfBirth = request.DateOfBirth
        };

        var card = new LibraryCard
        {
            UserId = user.Id,
            CardNumber = $"LIB-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            IssuedDate = DateTime.UtcNow,
            ExpiredDate = DateTime.UtcNow.AddYears(1),
            Status = "Active"
        };

        _context.Users.Add(user);
        _context.ReaderProfiles.Add(profile);
        _context.LibraryCards.Add(card);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Đăng ký tài khoản thành công",
            userId = user.Id,
            cardNumber = card.CardNumber
        });
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email không được để trống" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Mật khẩu không được để trống" });
        }

        var email = request.Email.Trim().ToLower();

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
        }

        if (user.Status == "Locked")
        {
            return Unauthorized(new { message = "Tài khoản đang bị khóa" });
        }

        var token = _jwtService.GenerateToken(user);

        var response = new AuthResponse
        {
            AccessToken = token,
            User = new AuthUserResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status
            }
        };

        return Ok(response);
    }

    [HttpPost("google-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { message = "Google ID Token không được để trống" });
        }

        var googleClientId = _configuration["GoogleAuth:ClientId"];

        if (string.IsNullOrWhiteSpace(googleClientId))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Backend chưa cấu hình GoogleAuth:ClientId trong appsettings.json"
            });
        }

        GoogleJsonWebSignature.Payload payload;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleClientId }
            };

            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch
        {
            return Unauthorized(new { message = "Google ID Token không hợp lệ hoặc không xác thực được" });
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return Unauthorized(new { message = "Google không trả về email hợp lệ" });
        }

        if (!payload.EmailVerified)
        {
            return Unauthorized(new { message = "Email Google chưa được xác minh" });
        }

        var email = payload.Email.Trim().ToLower();
        var fullName = string.IsNullOrWhiteSpace(payload.Name)
            ? email
            : payload.Name.Trim();

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
        {
            user = new User
            {
                FullName = fullName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Role = "Reader",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            var googleSubject = string.IsNullOrWhiteSpace(payload.Subject)
                ? Guid.NewGuid().ToString("N")
                : payload.Subject;

            var studentCodeSuffix = googleSubject.Length > 8
                ? googleSubject[..8]
                : googleSubject;

            var profile = new ReaderProfile
            {
                UserId = user.Id,
                StudentCode = $"GG-{studentCodeSuffix}",
                Phone = string.Empty,
                Address = string.Empty,
                DateOfBirth = null
            };

            var card = new LibraryCard
            {
                UserId = user.Id,
                CardNumber = await GenerateLibraryCardNumberAsync(),
                IssuedDate = DateTime.UtcNow,
                ExpiredDate = DateTime.UtcNow.AddYears(1),
                Status = "Active"
            };

            _context.Users.Add(user);
            _context.ReaderProfiles.Add(profile);
            _context.LibraryCards.Add(card);

            await _context.SaveChangesAsync();
        }
        else
        {
            if (user.Status == "Locked")
            {
                return Unauthorized(new { message = "Tài khoản đang bị khóa" });
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = fullName;
                await _context.SaveChangesAsync();
            }
        }

        var token = _jwtService.GenerateToken(user);

        var response = new AuthResponse
        {
            AccessToken = token,
            User = new AuthUserResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status
            }
        };

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        var user = await _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        return Ok(new
        {
            id = user.Id,
            fullName = user.FullName,
            email = user.Email,
            role = user.Role,
            status = user.Status,
            readerProfile = user.ReaderProfile == null ? null : new
            {
                id = user.ReaderProfile.Id,
                studentCode = user.ReaderProfile.StudentCode,
                phone = user.ReaderProfile.Phone,
                address = user.ReaderProfile.Address,
                dateOfBirth = user.ReaderProfile.DateOfBirth
            },
            libraryCard = user.LibraryCard == null ? null : new
            {
                id = user.LibraryCard.Id,
                cardNumber = user.LibraryCard.CardNumber,
                issuedDate = user.LibraryCard.IssuedDate,
                expiredDate = user.LibraryCard.ExpiredDate,
                status = user.LibraryCard.Status
            }
        });
    }
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile(UpdateProfileRequest request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "Họ tên không được để trống" });
        }

        var user = await _context.Users
            .Include(x => x.ReaderProfile)
            .Include(x => x.LibraryCard)
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        if (user.Status == "Locked")
        {
            return Unauthorized(new { message = "Tài khoản đang bị khóa" });
        }

        user.FullName = request.FullName.Trim();

        if (user.Role == "Reader")
        {
            if (user.ReaderProfile == null)
            {
                user.ReaderProfile = new ReaderProfile
                {
                    UserId = user.Id,
                    StudentCode = string.Empty
                };
            }

            user.ReaderProfile.Phone = request.Phone?.Trim() ?? string.Empty;
            user.ReaderProfile.Address = request.Address?.Trim() ?? string.Empty;
            user.ReaderProfile.DateOfBirth = request.DateOfBirth;
        }

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "UpdateProfile",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Người dùng {user.Email} đã cập nhật thông tin cá nhân");

        return Ok(new
        {
            message = "Cập nhật thông tin cá nhân thành công",
            user = new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                status = user.Status,
                readerProfile = user.ReaderProfile == null ? null : new
                {
                    id = user.ReaderProfile.Id,
                    studentCode = user.ReaderProfile.StudentCode,
                    phone = user.ReaderProfile.Phone,
                    address = user.ReaderProfile.Address,
                    dateOfBirth = user.ReaderProfile.DateOfBirth
                },
                libraryCard = user.LibraryCard == null ? null : new
                {
                    id = user.LibraryCard.Id,
                    cardNumber = user.LibraryCard.CardNumber,
                    issuedDate = user.LibraryCard.IssuedDate,
                    expiredDate = user.LibraryCard.ExpiredDate,
                    status = user.LibraryCard.Status
                }
            }
        });
    }
    [HttpPatch("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        if (string.IsNullOrWhiteSpace(request.OldPassword))
        {
            return BadRequest(new { message = "Mật khẩu cũ không được để trống" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });
        }

        if (request.OldPassword == request.NewPassword)
        {
            return BadRequest(new { message = "Mật khẩu mới không được trùng với mật khẩu cũ" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        if (user.Status == "Locked")
        {
            return Unauthorized(new { message = "Tài khoản đang bị khóa" });
        }

        var isOldPasswordValid = BCrypt.Net.BCrypt.Verify(
            request.OldPassword,
            user.PasswordHash);

        if (!isOldPasswordValid)
        {
            return BadRequest(new { message = "Mật khẩu cũ không đúng" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await _context.SaveChangesAsync();

        await _auditLogService.WriteAsync(
            action: "ChangePassword",
            entityName: "User",
            entityId: user.Id.ToString(),
            description: $"Người dùng {user.Email} đã đổi mật khẩu");

        return Ok(new
        {
            message = "Đổi mật khẩu thành công"
        });
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
}