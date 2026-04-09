using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/rooms")]
public class PlayerRoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DockerGameServerService _docker;
    private readonly IConfiguration _config;

    // Port range dành cho player rooms (không đụng system servers 27015-27020)
    private const int PortMin = 27100;
    private const int PortMax = 27199;

    public PlayerRoomsController(AppDbContext db, DockerGameServerService docker, IConfiguration config)
    {
        _db     = db;
        _docker = docker;
        _config = config;
    }

    // GET /api/rooms — danh sách phòng đang Active
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rooms = await _db.PlayerRooms
            .Where(r => r.Status == "Active")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.HostPlayerId, r.RoomName, r.MapName,
                r.Format, r.MaxPlayers, r.Password,
                r.Port, r.Status, r.CreatedAt,
                hasPassword = r.Password != null
            })
            .ToListAsync();
        return Ok(rooms);
    }

    // POST /api/rooms — tạo phòng + spawn Docker container (27100-27199)
    // Yêu cầu: tài khoản phải đã KYC Approved
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomDto dto)
    {
        // ── KYC Guard: chỉ tài khoản đã xác minh mới được tạo phòng ──────────
        if (string.IsNullOrWhiteSpace(dto.HostPlayerId))
            return BadRequest(new { error = "hostPlayerId không hợp lệ" });

        var kyc = await _db.KycSubmissions
            .FirstOrDefaultAsync(k => k.PlayerId == dto.HostPlayerId && k.Status == "Approved");

        if (kyc == null)
            return StatusCode(403, new { error = "Bạn cần hoàn thành xác minh KYC (được duyệt) mới có thể tạo phòng." });

        // ── Allocate port 27100-27199 ─────────────────────────────────────────
        var usedPorts = await _db.PlayerRooms
            .Where(r => r.Status == "Active")
            .Select(r => r.Port)
            .ToListAsync();

        int? port = null;
        for (int p = PortMin; p <= PortMax; p++)
        {
            if (!usedPorts.Contains(p)) { port = p; break; }
        }
        if (port == null)
            return BadRequest(new { error = "Không còn slot phòng trống (tối đa 100 phòng)" });

        var rconPwd = "Rcon_Cs16VN_2026!";

        var room = new PlayerRoom
        {
            HostPlayerId = dto.HostPlayerId,
            RoomName     = dto.RoomName,
            MapName      = dto.MapName,
            Format       = dto.Format,
            MaxPlayers   = dto.MaxPlayers,
            Password     = dto.Password,
            Port         = port,
            ContainerId  = $"cs16-room-{port}",
            Status       = "Active"
        };

        _db.PlayerRooms.Add(room);
        await _db.SaveChangesAsync();

        // ── Spawn container via DockerGameServerService ──────────────────────
        // Logic kết nối server-client giữ nguyên hoàn toàn:
        // container join network modern-cs16_default, bind port UDP+TCP,
        // mount AMX plugins dùng chung — không thay đổi gì ở đây.
        try
        {
            var dbConn = _config["DB_CONNECTION"] ?? "";
            var jwt    = _config["JWT_SECRET"]    ?? "";

            var (containerName, internalIp) = await _docker.StartRoomAsync(
                room.Id, room.RoomName, port.Value,
                room.MapName, room.MaxPlayers, rconPwd, dbConn, jwt);

            room.ContainerId = containerName;
            room.RconHost    = internalIp;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Giữ DB record nhưng log lỗi container
            return Ok(new
            {
                room.Id, room.RoomName, room.Port,
                warning = $"Phòng đã tạo nhưng container lỗi: {ex.Message}"
            });
        }

        return Ok(new
        {
            room.Id, room.HostPlayerId, room.RoomName, room.MapName,
            room.Format, room.MaxPlayers, room.Port,
            containerName = room.ContainerId, internalIp = room.RconHost
        });
    }

    // DELETE /api/rooms/{id}?playerId={hostPlayerId} — đóng phòng + remove container
    // Chỉ chủ phòng mới được xóa. Container guard được xử lý ở DockerGameServerService.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Close(Guid id, [FromQuery] string? playerId)
    {
        var room = await _db.PlayerRooms.FindAsync(id);
        if (room == null) return NotFound();

        // ── Owner check ───────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(playerId)
            && room.HostPlayerId != null
            && room.HostPlayerId != playerId)
        {
            return StatusCode(403, new { error = "Bạn không phải chủ phòng này." });
        }

        room.Status = "Closed";
        await _db.SaveChangesAsync();

        // DockerGameServerService.RemoveContainerAsync có guard prefix cs16-room-*
        // → không bao giờ xóa nhầm container gốc (italy/dust2/inferno/nuke)
        if (!string.IsNullOrEmpty(room.ContainerId))
            _ = _docker.RemoveContainerAsync(room.ContainerId);

        return Ok(new { closed = true });
    }

    // POST /api/rooms/{id}/kick — kick player qua RCON
    [HttpPost("{id:guid}/kick")]
    public async Task<IActionResult> KickPlayer(Guid id, [FromBody] KickDto dto)
    {
        var room = await _db.PlayerRooms.FindAsync(id);
        if (room == null) return NotFound();
        if (room.Port == null) return BadRequest(new { error = "Phòng chưa có port" });

        // Dùng RconHost (container internal IP) thay vì 127.0.0.1
        var rconIp = room.RconHost ?? "127.0.0.1";
        try
        {
            var response = await GoldSrcRcon.Execute(
                rconIp, room.Port.Value, "Rcon_Cs16VN_2026!", $"kick #{dto.UserId}");
            return Ok(new { kicked = true, response });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/rooms/{id}/broadcast — admin RCON say vào in-game chat
    [HttpPost("{id:guid}/broadcast")]
    public async Task<IActionResult> Broadcast(Guid id, [FromBody] BroadcastDto dto)
    {
        var room = await _db.PlayerRooms.FindAsync(id);
        if (room == null) return NotFound();
        if (room.Port == null) return BadRequest(new { error = "Phòng chưa có port" });

        var rconIp = room.RconHost ?? "127.0.0.1";
        string? rconResponse = null;

        try
        {
            // Gửi vào in-game chat qua RCON
            rconResponse = await GoldSrcRcon.Execute(
                rconIp, room.Port.Value, "Rcon_Cs16VN_2026!", $"say [ADMIN] {dto.Message}");
        }
        catch { /* RCON fail không block lưu message */ }

        // Lưu vào SpectatorMessages để spectators thấy
        _db.SpectatorMessages.Add(new SpectatorMessage
        {
            RoomId           = id.ToString(),
            RoomType         = "room",
            SenderName       = dto.SenderName ?? "ADMIN",
            Message          = $"[BROADCAST] {dto.Message}",
            IsAdminBroadcast = true,
        });
        await _db.SaveChangesAsync();

        return Ok(new { sent = true, rconResponse });
    }

    // GET /api/rooms/{id}/spec-chat?since={ticks} — spectator chat (poll)
    [HttpGet("{id:guid}/spec-chat")]
    public async Task<IActionResult> GetSpecChat(Guid id, [FromQuery] long since = 0)
    {
        var sinceTime = since > 0
            ? new DateTime(since, DateTimeKind.Utc)
            : DateTime.UtcNow.AddMinutes(-30);

        var messages = await _db.SpectatorMessages
            .Where(m => m.RoomId == id.ToString() && m.RoomType == "room"
                     && m.CreatedAt > sinceTime)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id, m.SenderName, m.Message,
                m.IsAdminBroadcast,
                ts = m.CreatedAt.Ticks
            })
            .ToListAsync();

        return Ok(messages);
    }

    // POST /api/rooms/{id}/spec-chat — gửi message spectator
    [HttpPost("{id:guid}/spec-chat")]
    public async Task<IActionResult> PostSpecChat(Guid id, [FromBody] SpecChatDto dto)
    {
        var room = await _db.PlayerRooms.FindAsync(id);
        if (room == null) return NotFound();

        _db.SpectatorMessages.Add(new SpectatorMessage
        {
            RoomId     = id.ToString(),
            RoomType   = "room",
            SenderName = dto.SenderName,
            Message    = dto.Message.Length > 500 ? dto.Message[..500] : dto.Message,
        });
        await _db.SaveChangesAsync();
        return Ok(new { sent = true });
    }
}

// ── DTOs ────────────────────────────────────────────────────────────────────
public record CreateRoomDto(
    string HostPlayerId, string RoomName, string MapName,
    string Format, int MaxPlayers, string? Password);
public record KickDto(string UserId);
public record BroadcastDto(string Message, string? SenderName);
public record SpecChatDto(string SenderName, string Message);
