using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/kyc")]
public class KycController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public KycController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db; _env = env;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromQuery] string playerId, IFormFile? cccdImage, IFormFile? selfieVideo)
    {
        if (string.IsNullOrEmpty(playerId)) return BadRequest(new { error = "playerId required" });

        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "kyc", playerId);
        Directory.CreateDirectory(uploadsDir);

        string? cccdPath = null, videoPath = null;

        if (cccdImage != null)
        {
            var ext = Path.GetExtension(cccdImage.FileName);
            cccdPath = Path.Combine(uploadsDir, $"cccd{ext}");
            await using var fs = System.IO.File.Create(cccdPath);
            await cccdImage.CopyToAsync(fs);
        }

        if (selfieVideo != null)
        {
            var ext = Path.GetExtension(selfieVideo.FileName);
            videoPath = Path.Combine(uploadsDir, $"selfie{ext}");
            await using var fs = System.IO.File.Create(videoPath);
            await selfieVideo.CopyToAsync(fs);
        }

        var existing = await _db.KycSubmissions.FirstOrDefaultAsync(k => k.PlayerId == playerId);
        if (existing != null)
        {
            if (cccdPath != null) existing.CccdImagePath = cccdPath;
            if (videoPath != null) existing.SelfieVideoPath = videoPath;
            existing.Status = "Pending";
            existing.SubmittedAt = DateTime.UtcNow;
        }
        else
        {
            _db.KycSubmissions.Add(new KycSubmission
            {
                PlayerId = playerId,
                CccdImagePath = cccdPath,
                SelfieVideoPath = videoPath
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã gửi hồ sơ KYC, vui lòng chờ xác minh!" });
    }

    [HttpGet("status/{playerId}")]
    public async Task<IActionResult> Status(string playerId)
    {
        var kyc = await _db.KycSubmissions.FirstOrDefaultAsync(k => k.PlayerId == playerId);
        if (kyc == null) return Ok(new { status = "NotSubmitted" });
        return Ok(new { kyc.Status, kyc.ReviewerNote, kyc.SubmittedAt });
    }

    // GET /api/kyc/my-status?playerId={id} — dùng trong Launcher
    [HttpGet("my-status")]
    public async Task<IActionResult> MyStatus([FromQuery] string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return BadRequest(new { error = "playerId required" });
        var kyc = await _db.KycSubmissions.FirstOrDefaultAsync(k => k.PlayerId == playerId);
        if (kyc == null) return Ok(new { status = "NotSubmitted", isApproved = false });
        return Ok(new { status = kyc.Status, isApproved = kyc.Status == "Approved", reviewerNote = kyc.ReviewerNote });
    }

    // Admin endpoints
    [HttpGet("queue")]
    public async Task<IActionResult> Queue()
    {
        var list = await _db.KycSubmissions
            .Where(k => k.Status == "Pending")
            .OrderBy(k => k.SubmittedAt)
            .Select(k => new { k.Id, k.PlayerId, k.Status, k.SubmittedAt,
                hasCccd = k.CccdImagePath != null,
                hasVideo = k.SelfieVideoPath != null })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("{id}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewDto dto)
    {
        var kyc = await _db.KycSubmissions.FindAsync(id);
        if (kyc == null) return NotFound();
        kyc.Status = dto.Approved ? "Approved" : "Rejected";
        kyc.ReviewerNote = dto.Note;
        kyc.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { kyc.Status });
    }

    [HttpGet("{id}/cccd")]
    public async Task<IActionResult> GetCccd(Guid id)
    {
        var kyc = await _db.KycSubmissions.FindAsync(id);
        if (kyc?.CccdImagePath == null || !System.IO.File.Exists(kyc.CccdImagePath)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(kyc.CccdImagePath);
        var ext = Path.GetExtension(kyc.CccdImagePath).ToLower();
        var ct = ext is ".png" ? "image/png" : "image/jpeg";
        return File(bytes, ct);
    }

    [HttpGet("{id}/video")]
    public async Task<IActionResult> GetVideo(Guid id)
    {
        var kyc = await _db.KycSubmissions.FindAsync(id);
        if (kyc?.SelfieVideoPath == null || !System.IO.File.Exists(kyc.SelfieVideoPath)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(kyc.SelfieVideoPath);
        return File(bytes, "video/mp4");
    }
}

public record ReviewDto(bool Approved, string? Note);
