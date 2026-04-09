using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/servers")]
public class ServersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DockerGameServerService _docker;
    private readonly IConfiguration _config;

    public ServersController(AppDbContext db, DockerGameServerService docker, IConfiguration config)
    {
        _db     = db;
        _docker = docker;
        _config = config;
    }

    // GET /api/servers  — danh sách tất cả server
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var servers = await _db.GameServers
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Id, s.Name, s.Host, s.Port,
                s.CurrentMap, s.Description,
                s.IsActive, s.MaxPlayers, s.CreatedAt,
                isManaged = s.ContainerName != null   // phân biệt managed vs original
            })
            .ToListAsync();
        return Ok(servers);
    }

    // GET /api/servers/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var s = await _db.GameServers.FindAsync(id);
        if (s == null) return NotFound();
        return Ok(new
        {
            s.Id, s.Name, s.Host, s.Port,
            s.CurrentMap, s.Description,
            s.IsActive, s.MaxPlayers,
            isManaged = s.ContainerName != null
        });
    }

    // POST /api/servers  — tạo server mới + spawn Docker container
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServerDto dto)
    {
        var server = new GameServer
        {
            Name         = dto.Name,
            Host         = dto.Host,
            Port         = dto.Port,
            RconPassword = dto.RconPassword ?? "",
            CurrentMap   = dto.CurrentMap,
            Description  = dto.Description ?? "",
            MaxPlayers   = dto.MaxPlayers,
            IsActive     = dto.IsActive
        };

        _db.GameServers.Add(server);
        await _db.SaveChangesAsync();  // cần ID trước khi tạo container

        // ── Spawn Docker container ───────────────────────────────────────────
        try
        {
            var dbConn  = _config["DB_CONNECTION"] ?? "";
            var jwtKey  = _config["JWT_SECRET"]    ?? "";
            var rconPwd = string.IsNullOrEmpty(server.RconPassword)
                ? (_config["RCON_PASSWORD"] ?? "Rcon_Cs16VN_2026!")
                : server.RconPassword;

            var (containerName, internalIp) = await _docker.StartServerAsync(
                server.Id, server.Name, server.Port,
                server.CurrentMap, server.MaxPlayers,
                rconPwd, dbConn, jwtKey);

            // Lưu container info và RconHost vào DB
            server.ContainerName = containerName;
            server.RconHost      = internalIp;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                server.Id, server.Name, server.Host, server.Port,
                containerName, internalIp,
                message = $"Container {containerName} đã được khởi động"
            });
        }
        catch (Exception ex)
        {
            // Nếu Docker fail, vẫn giữ DB record nhưng báo lỗi
            return Ok(new
            {
                server.Id, server.Name, server.Host, server.Port,
                containerName = (string?)null,
                warning = $"Server đã lưu nhưng không thể tạo container: {ex.Message}"
            });
        }
    }

    // PUT /api/servers/{id}  — cập nhật server (chỉ metadata, không restart container)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServerDto dto)
    {
        var server = await _db.GameServers.FindAsync(id);
        if (server == null) return NotFound();

        server.Name        = dto.Name;
        server.Host        = dto.Host;
        server.Port        = dto.Port;
        server.RconPassword = dto.RconPassword ?? server.RconPassword;
        server.CurrentMap  = dto.CurrentMap;
        server.Description = dto.Description ?? server.Description;
        server.MaxPlayers  = dto.MaxPlayers;
        server.IsActive    = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(new { server.Id, server.Name });
    }

    // DELETE /api/servers/{id}
    // Chỉ stop/remove container nếu là "managed" container (tạo bởi Dashboard)
    // KHÔNG đụng 6 container gốc
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var server = await _db.GameServers.FindAsync(id);
        if (server == null) return NotFound();

        // ── Nếu có managed container → stop và remove ────────────────────────
        if (!string.IsNullOrEmpty(server.ContainerName))
        {
            var removed = await _docker.RemoveServerAsync(server.ContainerName);
            if (!removed)
            {
                // Log nhưng vẫn xóa DB record
            }
        }
        // Nếu ContainerName == null → server gốc → KHÔNG làm gì với Docker

        _db.GameServers.Remove(server);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = true, containerRemoved = server.ContainerName != null });
    }
}

public record ServerDto(
    string Name,
    string Host,
    int Port,
    string CurrentMap,
    string? RconPassword,
    string? Description,
    int MaxPlayers,
    bool IsActive
);
