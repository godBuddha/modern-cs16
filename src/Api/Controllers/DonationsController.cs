using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api")]
public class DonationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public DonationsController(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env)
    {
        _db = db; _config = cfg; _env = env;
    }

    // GET /api/donations
    [HttpGet("donations")]
    public async Task<IActionResult> GetDonations([FromQuery] string? status, [FromQuery] int page = 1)
    {
        var q = _db.Donations.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(d => d.Status == status);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * 20).Take(20).ToListAsync();
        var sum = await _db.Donations.Where(d => d.Status == "Confirmed").SumAsync(d => d.Amount);
        return Ok(new { items, total, totalConfirmed = sum, page });
    }

    // POST /api/donations
    [HttpPost("donations")]
    public async Task<IActionResult> CreateDonation([FromBody] DonationDto dto)
    {
        var vietqrRef = $"CS16-{DateTime.UtcNow:yyyyMMddHHmm}-{Random.Shared.Next(1000, 9999)}";
        var donation = new Donation
        {
            PlayerId = dto.PlayerId,
            PlayerName = dto.PlayerName ?? "Anonymous",
            Amount = dto.Amount,
            Message = dto.Message,
            VietQRRef = vietqrRef,
            DonationType = dto.DonationType ?? "developer",
            TournamentName = dto.TournamentName
        };
        _db.Donations.Add(donation);
        await _db.SaveChangesAsync();

        var bank = _config["BANK_CODE"] ?? "970436";
        var account = _config["BANK_ACCOUNT"] ?? "0000000000";
        var qrUrl = $"https://img.vietqr.io/image/{bank}-{account}-qr_only.png?amount={(long)dto.Amount}&addInfo={Uri.EscapeDataString(vietqrRef)}&accountName=CS16%20Server";

        return Ok(new { donation.Id, vietqrRef, qrUrl, message = "Chuyển khoản với nội dung: " + vietqrRef });
    }

    // POST /api/donations/{id}/upload-proof — upload bằng chứng thanh toán
    [HttpPost("donations/{id}/upload-proof")]
    public async Task<IActionResult> UploadProof(int id, IFormFile? proofFile)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation == null) return NotFound();
        if (proofFile == null) return BadRequest(new { error = "Không có file" });

        var dir = Path.Combine(_env.ContentRootPath, "uploads", "donations");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(proofFile.FileName);
        var fileName = $"proof_{id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var filePath = Path.Combine(dir, fileName);
        await using var fs = System.IO.File.Create(filePath);
        await proofFile.CopyToAsync(fs);

        donation.PaymentProofPath = filePath;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã tải lên bằng chứng thanh toán", fileName });
    }

    // PATCH /api/donations/{id}/confirm
    [HttpPatch("donations/{id}/confirm")]
    public async Task<IActionResult> ConfirmDonation(int id)
    {
        var d = await _db.Donations.FindAsync(id);
        if (d == null) return NotFound();
        d.Status = "Confirmed";
        if (!string.IsNullOrEmpty(d.PlayerId))
        {
            var stats = await _db.PlayerStats.FindAsync(d.PlayerId);
            if (stats != null) stats.Credits += (int)Math.Floor(d.Amount / 1000);
        }
        await _db.SaveChangesAsync();
        return Ok(new { d.Status, d.Amount });
    }

    // GET /api/server/status (giữ nguyên cho compatibility)
    [HttpGet("server/status")]
    public IActionResult ServerStatus()
    {
        return Ok(new { online = true, map = "de_dust2", players = 0, maxPlayers = 32, port = 27015 });
    }
}

public record DonationDto(
    string? PlayerId, string? PlayerName, decimal Amount, string? Message,
    string? DonationType, string? TournamentName);
// RconDto moved to RconController.cs
