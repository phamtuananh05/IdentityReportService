using IdentityReportService.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IdentityReportService.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expireMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"] ?? "120");

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new Exception("JWT Key is missing in appsettings.json");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("userId", user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("email", user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim("fullName", user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("role", user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}