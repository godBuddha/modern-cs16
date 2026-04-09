using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api")]
public class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    public PlayersController(AppDbContext db) => _db = db;

    [HttpGet("leaderboard")]
    public async Task<IActionResult> Leaderboard()
    {
        var top = await _db.PlayerStats
            .OrderByDescending(p => p.EloScore)
            .Take(50)
            .ToListAsync();
        return Ok(top.Select((p, i) => new { 
            rank = i + 1, 
            player = new {
                p.PlayerId, p.DisplayName, p.Kills, p.Deaths, p.Headshots,
                p.Wins, p.EloScore, p.Credits, p.IsBanned,
                kd = p.Deaths > 0 ? Math.Round((double)p.Kills / p.Deaths, 2) : (double)p.Kills
            }
        }));
    }

    // ── GET /api/players/stats  (alias leaderboard, dùng cho Launcher) ─────────
    [HttpGet("players/stats")]
    public async Task<IActionResult> Stats()
    {
        var top = await _db.PlayerStats
            .OrderByDescending(p => p.EloScore)
            .Take(100)
            .ToListAsync();
        return Ok(top.Select(p => new
        {
            p.DisplayName,
            p.Kills, p.Deaths, p.Wins, p.EloScore,
            kd = p.Deaths > 0 ? Math.Round((double)p.Kills / p.Deaths, 2) : (double)p.Kills
        }));
    }

    // ── GET /api/players/me  (stats của người dùng đang đăng nhập) ────────────
    [HttpGet("players/me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var stats = await _db.PlayerStats.FindAsync(userId);
        if (stats == null) return NotFound();

        // Tính rank
        var rank = await _db.PlayerStats
            .CountAsync(p => p.EloScore > stats.EloScore) + 1;

        return Ok(new
        {
            stats.DisplayName, stats.Kills, stats.Deaths, stats.Headshots,
            stats.Wins, stats.EloScore, stats.Credits, stats.IsBanned,
            rank,
            kd = stats.Deaths > 0 ? Math.Round((double)stats.Kills / stats.Deaths, 2) : (double)stats.Kills
        });
    }

    // ── GET /api/game/status  (số người online, ping đến game server) ─────────
    [HttpGet("game/status")]
    public IActionResult GameStatus()
    {
        // Số người online lấy theo cách đơn giản: đếm session trong 5 phút gần đây
        // (PlayerStats.UpdatedAt được cập nhật mỗi khi chơi)
        var fiveMinAgo = DateTime.UtcNow.AddMinutes(-5);
        var playersOnline = _db.PlayerStats.Count(p => p.UpdatedAt >= fiveMinAgo);

        return Ok(new
        {
            status = "online",
            playersOnline,
            serverIp = "play.cs16vn.com",   // sẽ được override bởi config.json ở Launcher
            port = 27015,
            map = "de_dust2"
        });
    }

    [HttpGet("players/{id}")]
    public async Task<IActionResult> GetPlayer(string id)
    {
        var stats = await _db.PlayerStats.FindAsync(id);
        if (stats == null) return NotFound();
        return Ok(stats);
    }

    // Called by AMX plugin to update stats
    [HttpPost("game/event")]
    public async Task<IActionResult> GameEvent([FromBody] GameEventDto dto)
    {
        // ── Detect ELO multiplier via source IP ───────────────────────────────
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        double eloMultiplier = await GetEloMultiplier(sourceIp);

        var stats = await _db.PlayerStats.FirstOrDefaultAsync(p => p.DisplayName == dto.AttackerId);

        if (stats != null)
        {
            stats.Kills     += dto.Kills;
            stats.Headshots += dto.Headshots;
            stats.Wins      += dto.Wins;

            // ELO base: +20 kill, +10 headshot bonus, +30 round win — scaled by multiplier
            double eloChange = (dto.Kills * 20 + dto.Headshots * 10 + dto.Wins * 30) * eloMultiplier;
            stats.EloScore   = Math.Max(0, stats.EloScore + eloChange);
            stats.Credits   += (int)Math.Floor(eloChange / 2);
            stats.UpdatedAt  = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(dto.VictimId))
        {
            var victim = await _db.PlayerStats.FirstOrDefaultAsync(p => p.DisplayName == dto.VictimId);
            if (victim != null)
            {
                victim.Deaths++;
                victim.EloScore  = Math.Max(0, victim.EloScore - 15 * eloMultiplier);
                victim.UpdatedAt = DateTime.UtcNow;
            }
            _db.KillLogs.Add(new KillLog
            {
                AttackerId = stats?.PlayerId ?? dto.AttackerId,
                VictimId   = victim?.PlayerId ?? dto.VictimId,
                Weapon     = dto.Weapon ?? "unknown",
                Headshot   = dto.Headshots > 0,
                MapName    = dto.MapName ?? "unknown"
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, eloMultiplier });
    }

    /// <summary>
    /// Xác định ELO multiplier dựa trên source IP của container gửi event.
    /// System servers và casual rooms → ×1.0
    /// Tournament match theo stage → ×2.0 / ×3.0 / ×4.0
    /// </summary>
    private async Task<double> GetEloMultiplier(string sourceIp)
    {
        if (string.IsNullOrEmpty(sourceIp)) return 1.0;

        // Kiểm tra có phải tournament match container không
        var match = await _db.TournamentMatches
            .Where(m => m.ContainerIp == sourceIp && m.Status == "Active")
            .Select(m => new { m.Stage })
            .FirstOrDefaultAsync();

        if (match != null)
        {
            return match.Stage switch
            {
                "Final"    => 4.0,
                "Knockout" => 3.0,
                "Group"    => 2.0,
                _          => 2.0,
            };
        }

        // Player room hoặc system server → ×1.0
        return 1.0;
    }

    // Admin: ban/unban player
    [HttpPost("players/{id}/ban")]
    [Authorize]
    public async Task<IActionResult> Ban(string id, [FromBody] BanDto dto)
    {
        var stats = await _db.PlayerStats.FindAsync(id);
        if (stats == null) return NotFound();
        stats.IsBanned = dto.Banned;
        stats.BanReason = dto.Reason;
        await _db.SaveChangesAsync();
        return Ok(new { banned = stats.IsBanned });
    }
}

public record GameEventDto(string AttackerId, string? VictimId, int Kills, int Headshots, int Wins, string? Weapon, string? MapName);
public record BanDto(bool Banned, string? Reason);
