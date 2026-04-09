using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Api.Controllers;

[ApiController]
[Route("api/rcon")]
public class RconController : ControllerBase
{
    private readonly AppDbContext _db;
    public RconController(AppDbContext db) => _db = db;

    // POST /api/rcon/command  — Body: { "command": "status", "serverId": 1 }
    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] RconCommandDto dto)
    {
        var servers = await _db.GameServers.Where(s => s.IsActive).OrderBy(s => s.Id).ToListAsync();
        if (!servers.Any()) return BadRequest(new { error = "Không có server nào đang hoạt động" });

        var server = dto.ServerId.HasValue
            ? servers.FirstOrDefault(s => s.Id == dto.ServerId.Value)
            : servers.First();

        if (server == null) return NotFound(new { error = "Không tìm thấy server" });

        var password = !string.IsNullOrEmpty(server.RconPassword)
            ? server.RconPassword : "Rcon_Cs16VN_2026!";

        // RconHost = Docker-internal IP (connects from API container)
        // Host     = External LAN IP (used by game clients)
        var rconHost = !string.IsNullOrEmpty(server.RconHost) ? server.RconHost : server.Host;

        try
        {
            var response = await GoldSrcRcon.Execute(rconHost, server.Port, password, dto.Command);
            return Ok(new { response, server = server.Name, serverId = server.Id });
        }
        catch (Exception ex)
        {
            return Ok(new { response = $"[RCON Error] {ex.Message}", server = server.Name, serverId = server.Id });
        }
    }

    // POST /api/rcon/message — say vào server
    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] RconMessageDto dto)
    {
        var server = await _db.GameServers.FindAsync(dto.ServerId);
        if (server == null) return NotFound();
        var password = !string.IsNullOrEmpty(server.RconPassword)
            ? server.RconPassword : "Rcon_Cs16VN_2026!";
        var rconHost = !string.IsNullOrEmpty(server.RconHost) ? server.RconHost : server.Host;
        try
        {
            var response = await GoldSrcRcon.Execute(rconHost, server.Port, password, $"say [ADMIN] {dto.Message}");
            return Ok(new { sent = true, response });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record RconCommandDto(string Command, int? ServerId);
public record RconMessageDto(int ServerId, string Message);

// ── GoldSrc RCON UDP Client ──────────────────────────────────────────────────
// Protocol: challenge/response over UDP (CS 1.6 / GoldSrc engine)
public static class GoldSrcRcon
{
    // GoldSrc packet header: 4 × 0xFF
    private static readonly byte[] Header = { 0xFF, 0xFF, 0xFF, 0xFF };

    // Build a GoldSrc packet: header + Latin1-encoded text
    // MUST use Latin1 (not ASCII) — ASCII corrupts bytes > 127
    private static byte[] BuildPacket(string text)
        => Header.Concat(Encoding.Latin1.GetBytes(text)).ToArray();

    public static async Task<string> Execute(
        string host, int port, string password, string command, int timeoutMs = 3000)
    {
        // Force IPv4 socket — host.docker.internal resolves to IPv6 on Mac Docker Desktop
        using var udp = new UdpClient(AddressFamily.InterNetwork);

        // Resolve hostname to IPv4
        IPAddress addr;
        try
        {
            var ips = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork);
            addr = ips.Length > 0 ? ips[0] : IPAddress.Parse(host);
        }
        catch
        {
            addr = IPAddress.Parse(host);
        }

        // Connect() binds to a stable ephemeral source port.
        // GoldSrc tracks challenge by source IP:port — port MUST stay constant
        // between the challenge request and the RCON command packet.
        var ep = new IPEndPoint(addr, port);
        udp.Connect(ep);

        // ── Step 1: Request challenge ────────────────────────────────────────
        var challengePacket = BuildPacket("challenge rcon\n");
        await udp.SendAsync(challengePacket, challengePacket.Length);

        string challenge;
        using (var cts = new CancellationTokenSource(timeoutMs))
        {
            try
            {
                var recv = await udp.ReceiveAsync(cts.Token);
                // Format: \xff\xff\xff\xffchallenge rcon <number>\n\0
                // (NO space between header and 'challenge' — split by space gives only 3 parts!)
                // Use Regex to robustly extract the numeric challenge value
                var raw = Encoding.Latin1.GetString(recv.Buffer);
                var m = Regex.Match(raw, @"\d+");
                challenge = m.Success ? m.Value : "0";
            }
            catch (OperationCanceledException)
            {
                return "[RCON] Server không phản hồi challenge (timeout 3s)";
            }
            catch (Exception ex)
            {
                return $"[RCON] Challenge error: {ex.Message}";
            }
        }

        // ── Step 2: Send RCON command ────────────────────────────────────────
        var rconPacket = BuildPacket($"rcon {challenge} \"{password}\" {command}\n");
        await udp.SendAsync(rconPacket, rconPacket.Length);

        // ── Step 3: Collect response (may arrive in multiple packets) ────────
        var sb = new StringBuilder();
        try
        {
            while (true)
            {
                using var innerCts = new CancellationTokenSource(timeoutMs);
                var recv = await udp.ReceiveAsync(innerCts.Token);
                var text = Encoding.Latin1.GetString(recv.Buffer);
                // Strip 5-byte GoldSrc header: 0xFF×4 + 'l'
                if (recv.Buffer.Length >= 5 && recv.Buffer[0] == 0xFF)
                    text = text[5..];
                sb.Append(text.TrimEnd('\0'));
                if (recv.Buffer.Length < 1400) break; // last packet in sequence
            }
        }
        catch (OperationCanceledException) { /* timeout = no more packets */ }
        catch (SocketException) { /* expected on last packet */ }

        var result = sb.ToString().Trim().TrimEnd('\0');
        return string.IsNullOrEmpty(result) ? "[OK] Command executed" : result;
    }
}
