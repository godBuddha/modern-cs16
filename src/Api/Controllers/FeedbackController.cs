using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly AppDbContext _db;
    public FeedbackController(AppDbContext db) => _db = db;

    // GET /api/feedback (admin)
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? type)
    {
        var q = _db.Feedbacks.AsQueryable();
        if (!string.IsNullOrEmpty(type)) q = q.Where(f => f.Type == type);
        var list = await q.OrderByDescending(f => f.CreatedAt).ToListAsync();
        return Ok(list);
    }

    // POST /api/feedback
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] FeedbackDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Nội dung không được trống" });

        var fb = new Feedback
        {
            PlayerId = dto.PlayerId,
            PlayerName = dto.PlayerName ?? "Anonymous",
            Type = dto.Type ?? "suggestion",
            Content = dto.Content
        };
        _db.Feedbacks.Add(fb);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Cảm ơn! Góp ý của bạn đã được ghi nhận.", id = fb.Id });
    }
}

public record FeedbackDto(string? PlayerId, string? PlayerName, string? Type, string Content);
