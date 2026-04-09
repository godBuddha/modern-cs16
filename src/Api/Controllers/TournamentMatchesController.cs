using Api.Data;
using Api.Models;
using Api.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.Controllers;

[ApiController]
[Route("api/tournaments/{tournamentId:guid}/matches")]
public class TournamentMatchesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DockerGameServerService _docker;
    private readonly IConfiguration _config;
    private readonly ILogger<TournamentMatchesController> _log;

    public TournamentMatchesController(AppDbContext db, DockerGameServerService docker,
        IConfiguration config, ILogger<TournamentMatchesController> log)
    {
        _db     = db;
        _docker = docker;
        _config = config;
        _log    = log;
    }

    // GET /api/tournaments/{id}/matches
    [HttpGet]
    public async Task<IActionResult> List(Guid tournamentId)
    {
        var matches = await _db.TournamentMatches
            .Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return Ok(matches);
    }

    // POST /api/tournaments/{id}/matches — tạo trận
    [HttpPost]
    public async Task<IActionResult> Create(Guid tournamentId, [FromBody] CreateMatchDto dto)
    {
        var tournament = await _db.Tournaments.FindAsync(tournamentId);
        if (tournament == null) return NotFound(new { error = "Không tìm thấy giải đấu" });

        var matchNumber = await _db.TournamentMatches.CountAsync(m => m.TournamentId == tournamentId) + 1;
        var shortId     = tournamentId.ToString()[..8];
        var roomCode    = $"CS16-TOUR-{shortId}-{matchNumber}";

        var match = new TournamentMatch
        {
            TournamentId = tournamentId,
            RoomCode     = roomCode,
            MapName      = dto.MapName,
            TeamA        = dto.TeamA,
            TeamB        = dto.TeamB,
            Stage        = dto.Stage ?? "Group",
            Status       = "Pending"
        };
        _db.TournamentMatches.Add(match);
        await _db.SaveChangesAsync();
        return Ok(match);
    }

    // POST /api/tournament-matches/{matchId}/start-room — spawn container + kích hoạt trận
    [HttpPost("/api/tournament-matches/{matchId:guid}/start-room")]
    public async Task<IActionResult> StartRoom(Guid matchId)
    {
        var match = await _db.TournamentMatches
            .Include(m => m.Tournament)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) return NotFound();
        if (match.Status == "Active") return BadRequest(new { error = "Trận đã đang chạy" });

        // ── Allocate port PAIR (game=even, hltv=game+1) trong dải 27200-27298 ─
        // Bước 2 để giữ game:27200, hltv:27201, game:27202, hltv:27203...
        var usedGamePorts = await _db.TournamentMatches
            .Where(m => m.Status == "Active")
            .Select(m => m.MatchPort)
            .ToListAsync();

        int? gamePort = null;
        for (int p = 27200; p <= 27298; p += 2)      // bước 2, chỉ cần kiểm tra port chẵn
        {
            if (!usedGamePorts.Contains(p)) { gamePort = p; break; }
        }
        if (gamePort == null)
            return BadRequest(new { error = "Hết port cho tournament (tối đa 50 trận đồng thời)" });

        var hltvPort = gamePort.Value + 1;             // HLTV luôn = matchPort + 1

        var dbConn  = _config["DB_CONNECTION"] ?? "";
        var jwt     = _config["JWT_SECRET"]    ?? "";
        var rconPwd = "Rcon_Cs16VN_2026!";
        var title   = match.Tournament?.Title ?? "Tournament";

        try
        {
            // 1. Spawn game server container (players connect vào đây — KHÔNG ĐỔI)
            var (containerName, containerIp) = await _docker.StartMatchAsync(
                match.Id, title, gamePort.Value,
                match.MapName, 20, rconPwd, dbConn, jwt);

            match.ContainerName  = containerName;
            match.ContainerIp    = containerIp;
            match.Port           = gamePort;
            match.MatchPort      = gamePort;
            match.Status         = "Active";
            match.HltvPort       = hltvPort;
            await _db.SaveChangesAsync();

            // 2. Spawn HLTV relay container (spectators connect vào đây)
            //    Fire-and-forget OK vì HLTV có sleep 12s bên trong, không cần block
            _ = Task.Run(async () =>
            {
                try
                {
                    var hltvName = await _docker.StartHltvAsync(
                        match.Id, match.RoomCode, containerIp, gamePort.Value, hltvPort);

                    // Lưu HLTV container name
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var db2 = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var m2  = await db2.TournamentMatches.FindAsync(match.Id);
                    if (m2 != null)
                    {
                        m2.HltvContainerName = hltvName;
                        await db2.SaveChangesAsync();
                    }
                    _log.LogInformation("[StartRoom] HLTV started: {HltvName} port={HltvPort}",
                        hltvName, hltvPort);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("[StartRoom] HLTV failed (non-fatal): {Err}", ex.Message);
                }
            });

            return Ok(new
            {
                match.Id, match.RoomCode, match.Stage,
                gamePort,
                hltvPort,
                containerName,
                containerIp,
                message = $"Match {match.RoomCode} started. Players: :{gamePort} | Spectators (HLTV): :{hltvPort} (30s delay)"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Không thể khởi động container: {ex.Message}" });
        }
    }

    // POST /api/tournament-matches/{matchId}/spec-chat — spectator chat
    [HttpPost("/api/tournament-matches/{matchId:guid}/spec-chat")]
    public async Task<IActionResult> PostSpecChat(Guid matchId, [FromBody] SpecChatDto dto)
    {
        _db.SpectatorMessages.Add(new SpectatorMessage
        {
            RoomId     = matchId.ToString(),
            RoomType   = "match",
            SenderName = dto.SenderName,
            Message    = dto.Message.Length > 500 ? dto.Message[..500] : dto.Message,
        });
        await _db.SaveChangesAsync();
        return Ok(new { sent = true });
    }

    // GET /api/tournament-matches/{matchId}/spec-chat?since={ticks}
    [HttpGet("/api/tournament-matches/{matchId:guid}/spec-chat")]
    public async Task<IActionResult> GetSpecChat(Guid matchId, [FromQuery] long since = 0)
    {
        var sinceTime = since > 0
            ? new DateTime(since, DateTimeKind.Utc)
            : DateTime.UtcNow.AddMinutes(-60);

        var messages = await _db.SpectatorMessages
            .Where(m => m.RoomId == matchId.ToString() && m.RoomType == "match"
                     && m.CreatedAt > sinceTime)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.SenderName, m.Message, m.IsAdminBroadcast, ts = m.CreatedAt.Ticks })
            .ToListAsync();

        return Ok(messages);
    }

    // POST /api/tournament-matches/{matchId}/broadcast — admin RCON say vào trận
    [HttpPost("/api/tournament-matches/{matchId:guid}/broadcast")]
    public async Task<IActionResult> Broadcast(Guid matchId, [FromBody] BroadcastDto dto)
    {
        var match = await _db.TournamentMatches.FindAsync(matchId);
        if (match == null) return NotFound();

        string? rconResponse = null;
        if (!string.IsNullOrEmpty(match.ContainerIp) && match.MatchPort.HasValue)
        {
            try
            {
                rconResponse = await GoldSrcRcon.Execute(
                    match.ContainerIp, match.MatchPort.Value,
                    "Rcon_Cs16VN_2026!", $"say [ADMIN] {dto.Message}");
            }
            catch { }
        }

        _db.SpectatorMessages.Add(new SpectatorMessage
        {
            RoomId           = matchId.ToString(),
            RoomType         = "match",
            SenderName       = dto.SenderName ?? "ADMIN",
            Message          = $"[BROADCAST] {dto.Message}",
            IsAdminBroadcast = true,
        });
        await _db.SaveChangesAsync();
        return Ok(new { sent = true, rconResponse });
    }

    // PATCH /api/tournament-matches/{matchId}/score
    [HttpPatch("/api/tournament-matches/{matchId:guid}/score")]
    public async Task<IActionResult> UpdateScore(Guid matchId, [FromBody] ScoreDto dto)
    {
        var match = await _db.TournamentMatches.FindAsync(matchId);
        if (match == null) return NotFound();
        match.ScoreA = dto.ScoreA;
        match.ScoreB = dto.ScoreB;
        if (dto.Status != null) match.Status = dto.Status;
        await _db.SaveChangesAsync();
        return Ok(match);
    }

    // GET /api/tournament-matches/{matchId}/export — xuất Excel 1 trận
    [HttpGet("/api/tournament-matches/{matchId:guid}/export")]
    public async Task<IActionResult> ExportMatch(Guid matchId)
    {
        var match = await _db.TournamentMatches
            .Include(m => m.Tournament)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null) return NotFound();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Kết quả trận");
        ws.Cell(1, 1).Value = "Giải đấu"; ws.Cell(1, 2).Value = match.Tournament?.Title;
        ws.Cell(2, 1).Value = "Mã phòng"; ws.Cell(2, 2).Value = match.RoomCode;
        ws.Cell(3, 1).Value = "Map"; ws.Cell(3, 2).Value = match.MapName;
        ws.Cell(4, 1).Value = "Trạng thái"; ws.Cell(4, 2).Value = match.Status;
        ws.Cell(6, 1).Value = "Team A"; ws.Cell(6, 2).Value = match.TeamA;
        ws.Cell(7, 1).Value = "Team B"; ws.Cell(7, 2).Value = match.TeamB;
        ws.Cell(8, 1).Value = "Tỉ số"; ws.Cell(8, 2).Value = $"{match.ScoreA} : {match.ScoreB}";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"tran_{match.RoomCode}.xlsx");
    }

    // GET /api/tournaments/{id}/export — xuất Excel toàn giải
    [HttpGet("/api/tournaments/{tournamentId:guid}/export")]
    public async Task<IActionResult> ExportTournament(Guid tournamentId)
    {
        var tournament = await _db.Tournaments
            .Include(t => t.Matches)
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);
        if (tournament == null) return NotFound();

        using var wb = new XLWorkbook();

        // Sheet 1: Thông tin giải
        var ws1 = wb.Worksheets.Add("Giải đấu");
        ws1.Cell(1, 1).Value = "Tên giải"; ws1.Cell(1, 2).Value = tournament.Title;
        ws1.Cell(2, 1).Value = "Thể thức"; ws1.Cell(2, 2).Value = tournament.Format;
        ws1.Cell(3, 1).Value = "Quy ước"; ws1.Cell(3, 2).Value = tournament.RoundSystem;
        ws1.Cell(4, 1).Value = "Trạng thái"; ws1.Cell(4, 2).Value = tournament.Status;
        ws1.Cell(5, 1).Value = "Người tổ chức"; ws1.Cell(5, 2).Value = tournament.OrganizerName;
        ws1.Cell(6, 1).Value = "Số người tham gia"; ws1.Cell(6, 2).Value = tournament.Registrations.Count;

        // Sheet 2: Danh sách trận
        var ws2 = wb.Worksheets.Add("Các trận đấu");
        ws2.Cell(1, 1).Value = "Mã phòng"; ws2.Cell(1, 2).Value = "Map";
        ws2.Cell(1, 3).Value = "Team A"; ws2.Cell(1, 4).Value = "Team B";
        ws2.Cell(1, 5).Value = "Tỉ số A"; ws2.Cell(1, 6).Value = "Tỉ số B";
        ws2.Cell(1, 7).Value = "Trạng thái";
        int row = 2;
        foreach (var m in tournament.Matches.OrderBy(m => m.CreatedAt))
        {
            ws2.Cell(row, 1).Value = m.RoomCode;
            ws2.Cell(row, 2).Value = m.MapName;
            ws2.Cell(row, 3).Value = m.TeamA;
            ws2.Cell(row, 4).Value = m.TeamB;
            ws2.Cell(row, 5).Value = m.ScoreA;
            ws2.Cell(row, 6).Value = m.ScoreB;
            ws2.Cell(row, 7).Value = m.Status;
            row++;
        }
        ws2.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"giai_{tournament.Title.Replace(" ", "_")}.xlsx");
    }
}

public record CreateMatchDto(string MapName, string TeamA, string TeamB, string? Stage);
public record ScoreDto(int ScoreA, int ScoreB, string? Status);

// Extend TournamentController DTO
public partial class TournamentExtensions
{
    public record TournamentFullDto(
        string Title, string? Description, decimal EntryFee, int MaxPlayers, DateTime? StartDate,
        string Format, string RoundSystem, string MapList, string OrganizerName, string OrganizerId, int PlayersPerMap);
}
