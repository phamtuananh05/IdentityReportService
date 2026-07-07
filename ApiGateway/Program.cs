using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Đọc ocelot.json và thay thế ${VAR} bằng biến môi trường
var ocelotPath = Path.Combine(Directory.GetCurrentDirectory(), "ocelot.json");
var ocelotContent = File.ReadAllText(ocelotPath);

// Thay thế các biến môi trường
ocelotContent = System.Text.RegularExpressions.Regex.Replace(
    ocelotContent,
    @"\$\{([^}]+)\}",
    match =>
    {
        var varName = match.Groups[1].Value;
        return Environment.GetEnvironmentVariable(varName) ?? match.Value;
    });

// Ghi file tạm thời
var tempOcelotPath = Path.Combine(Path.GetTempPath(), "ocelot.resolved.json");
File.WriteAllText(tempOcelotPath, ocelotContent);

builder.Configuration.AddJsonFile(tempOcelotPath, optional: false, reloadOnChange: false);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new Exception("JWT Key is missing in appsettings.json");
}

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowFrontend");

// Chặn OPTIONS preflight trước khi đi vào Authentication/Ocelot
app.Use(async (context, next) =>
{
    if (context.Request.Method == HttpMethods.Options)
    {
        var origin = context.Request.Headers.Origin.ToString();

        if (!string.IsNullOrWhiteSpace(origin))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        }
        else
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        }

        context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,PATCH,OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization,Content-Type,X-Internal-Service-Key";
        context.Response.Headers["Access-Control-Max-Age"] = "86400";

        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

await app.UseOcelot();

app.Run();