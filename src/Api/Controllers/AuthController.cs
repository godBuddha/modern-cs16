using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> um, AppDbContext db, IConfiguration cfg)
    {
        _userManager = um; _db = db; _config = cfg;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Username,
            Email = dto.Email,
            DisplayName = dto.Username
        };
        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        // Create stats
        _db.PlayerStats.Add(new PlayerStats { PlayerId = user.Id, DisplayName = user.DisplayName });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đăng ký thành công!", userId = user.Id });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        ApplicationUser? user = null;
        // Support login by email or username
        if (dto.Login.Contains('@'))
            user = await _userManager.FindByEmailAsync(dto.Login);
        else
            user = await _userManager.FindByNameAsync(dto.Login);

        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { error = "Sai tên đăng nhập hoặc mật khẩu" });

        var token = GenerateJwt(user);
        return Ok(new { token, userId = user.Id, displayName = user.DisplayName });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Ok(new { message = "Nếu email tồn tại, link khôi phục đã được gửi." });

        // Dummy implementation for now. Real app would generate password reset token and send email.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        Console.WriteLine($"[Dev Only] Reset token for {dto.Email}: {token}");
        
        return Ok(new { message = "Nếu email tồn tại, link khôi phục đã được gửi." });
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify([FromQuery] string token)
    {
        try
        {
            var secret = _config["JWT_SECRET"] ?? "change_this_to_a_very_long_random_secret_key";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return Ok();
        }
        catch { return Unauthorized(); }
    }

    // ── DEV ONLY: direct password reset ──────────────────────────────────────
    [HttpPost("dev/reset-password")]
    public async Task<IActionResult> DevResetPassword([FromBody] DevResetDto dto)
    {
        var user = await _userManager.FindByNameAsync(dto.Username);
        if (user == null) return NotFound("User không tồn tại");
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, dto.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));
        return Ok(new { message = $"Đã reset mật khẩu cho {dto.Username}" });
    }

    private string GenerateJwt(ApplicationUser user)
    {
        var secret = _config["JWT_SECRET"] ?? "change_this_to_a_very_long_random_secret_key";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim("displayName", user.DisplayName)
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// DTOs
public record RegisterDto(string Username, string Email, string Password);
public record LoginDto(string Login, string Password);
public record ForgotPasswordDto(string Email);
public record DevResetDto(string Username, string NewPassword);

