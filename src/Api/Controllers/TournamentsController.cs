using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/tournaments")]
public class TournamentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TournamentsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = _db.Tournaments.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);
        var list = await q.OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.Id, t.Title, t.Description, t.EntryFee, t.PrizePool,
                t.Status, t.MaxPlayers, t.StartDate, t.CreatedAt,
                t.Format, t.RoundSystem, t.MapList, t.OrganizerName, t.PlayersPerMap,
                registeredCount = t.Registrations.Count,
                matchCount = t.Matches.Count
            }).ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var t = await _db.Tournaments
            .Include(t => t.Registrations)
            .Include(t => t.Matches)
            .FirstOrDefaultAsync(t => t.Id == id);
        return t == null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TournamentDto dto)
    {
        // Validate KYC nếu organizer cung cấp playerId
        if (!string.IsNullOrEmpty(dto.OrganizerId))
        {
            var kyc = await _db.KycSubmissions.FirstOrDefaultAsync(k => k.PlayerId == dto.OrganizerId);
            if (kyc?.Status != "Approved")
                return BadRequest(new { error = "Tài khoản chưa được xác minh KYC để tạo giải đấu" });
        }

        var t = new Tournament
        {
            Title = dto.Title,
            Description = dto.Description,
            EntryFee = dto.EntryFee,
            MaxPlayers = dto.MaxPlayers,
            StartDate = dto.StartDate,
            Format = dto.Format ?? "5vs5",
            RoundSystem = dto.RoundSystem ?? "5round",
            MapList = dto.MapList ?? "[]",
            OrganizerName = dto.OrganizerName ?? "",
            OrganizerId = dto.OrganizerId ?? "",
            PlayersPerMap = dto.PlayersPerMap
        };
        _db.Tournaments.Add(t);
        await _db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TournamentDto dto)
    {
        var t = await _db.Tournaments.FindAsync(id);
        if (t == null) return NotFound();
        t.Title = dto.Title; t.Description = dto.Description;
        t.EntryFee = dto.EntryFee; t.MaxPlayers = dto.MaxPlayers;
        t.StartDate = dto.StartDate;
        if (dto.Format != null) t.Format = dto.Format;
        if (dto.RoundSystem != null) t.RoundSystem = dto.RoundSystem;
        if (dto.MapList != null) t.MapList = dto.MapList;
        if (dto.OrganizerName != null) t.OrganizerName = dto.OrganizerName;
        t.PlayersPerMap = dto.PlayersPerMap;
        await _db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] StatusDto dto)
    {
        var t = await _db.Tournaments.FindAsync(id);
        if (t == null) return NotFound();
        t.Status = dto.Status;
        await _db.SaveChangesAsync();
        return Ok(new { t.Id, t.Status });
    }

    [HttpPost("{id}/register")]
    public async Task<IActionResult> Register(Guid id, [FromBody] RegisterPlayerDto dto)
    {
        var t = await _db.Tournaments.Include(t => t.Registrations).FirstOrDefaultAsync(t => t.Id == id);
        if (t == null) return NotFound();
        if (t.Status != "Open") return BadRequest(new { error = "Giải đấu không mở đăng ký" });
        if (t.Registrations.Count >= t.MaxPlayers) return BadRequest(new { error = "Giải đấu đã đủ người" });
        if (t.Registrations.Any(r => r.PlayerId == dto.PlayerId))
            return BadRequest(new { error = "Đã đăng ký rồi" });

        if (t.EntryFee > 0)
        {
            var stats = await _db.PlayerStats.FindAsync(dto.PlayerId);
            if (stats == null || stats.Credits < (int)t.EntryFee)
                return BadRequest(new { error = "Không đủ credits" });
            stats.Credits -= (int)t.EntryFee;
            t.PrizePool += t.EntryFee * 0.9m;
        }

        _db.TournamentRegistrations.Add(new TournamentRegistration { TournamentId = id, PlayerId = dto.PlayerId });
        await _db.SaveChangesAsync();
        return Ok(new { message = "Đăng ký thành công!" });
    }
}

public record TournamentDto(
    string Title, string? Description, decimal EntryFee, int MaxPlayers, DateTime? StartDate,
    string? Format, string? RoundSystem, string? MapList,
    string? OrganizerName, string? OrganizerId, int PlayersPerMap = 5);
public record StatusDto(string Status);
public record RegisterPlayerDto(string PlayerId);
