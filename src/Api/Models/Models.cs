using Microsoft.AspNetCore.Identity;

namespace Api.Models;

// ASP.NET Core Identity user (extends IdentityUser)
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "Player";
    public PlayerStats? Stats { get; set; }
    public KycSubmission? Kyc { get; set; }
}

public class PlayerStats
{
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "Player";
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Headshots { get; set; }
    public int Wins { get; set; }
    public double EloScore { get; set; } = 1000.0;
    public int Credits { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser? Player { get; set; }
}

public class KillLog
{
    public long Id { get; set; }
    public string AttackerId { get; set; } = "";
    public string VictimId { get; set; } = "";
    public string Weapon { get; set; } = "";
    public bool Headshot { get; set; }
    public string MapName { get; set; } = "unknown";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Tournament & Match ──────────────────────────────────────────────────────
public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal EntryFee { get; set; }
    public decimal PrizePool { get; set; }
    public string Status { get; set; } = "Open"; // Open/Ongoing/Finished/Cancelled
    public int MaxPlayers { get; set; } = 16;
    public DateTime? StartDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // New v1.1 fields
    public string Format { get; set; } = "5vs5";           // 1vs1/3vs3/5vs5/10vs10
    public string RoundSystem { get; set; } = "5round";    // 5round/10round
    public string MapList { get; set; } = "[]";            // JSON array of map names
    public string OrganizerName { get; set; } = "";
    public string OrganizerId { get; set; } = "";
    public int PlayersPerMap { get; set; } = 5;

    public List<TournamentRegistration> Registrations { get; set; } = new();
    public List<TournamentMatch> Matches { get; set; } = new();
}

public class TournamentMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public string RoomCode { get; set; } = "";          // CS16-TOUR-{shortId}-{n}
    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }         // Docker container name
    public string? ContainerIp { get; set; }           // Docker internal IP (for ELO context)
    public int? Port { get; set; }                     // 27200-27299 (tournament range)
    public int? MatchPort { get; set; }                // same as Port, for clarity
    public string Stage { get; set; } = "Group";       // Group / Knockout / Final (ELO multiplier)
    public string MapName { get; set; } = "de_dust2";
    public string TeamA { get; set; } = "";
    public string TeamB { get; set; } = "";
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public string Status { get; set; } = "Pending";    // Pending/Active/Finished
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // HLTV Relay (spectators kết nối vào hltvPort, không vào matchPort)
    public int? HltvPort { get; set; }                 // 27201/27203/... (matchPort + 1)
    public string? HltvContainerName { get; set; }     // cs16-hltv-{hltvPort}
    public Tournament? Tournament { get; set; }
}

public class TournamentRegistration
{
    public Guid TournamentId { get; set; }
    public string PlayerId { get; set; } = "";
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public Tournament? Tournament { get; set; }
}

// ── Player Rooms ─────────────────────────────────────────────────────────────
public class PlayerRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HostPlayerId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string MapName { get; set; } = "de_dust2";
    public string Format { get; set; } = "5vs5";        // 1vs1/3vs3/5vs5/10vs10
    public int MaxPlayers { get; set; } = 10;
    public string? Password { get; set; }
    public int? Port { get; set; }                      // 27100-27199 (player room range)
    public string? ContainerId { get; set; }            // Docker container name
    public string? RconHost { get; set; }               // Docker internal IP for RCON
    public string Status { get; set; } = "Active";      // Active/Closed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Spectator Messages ────────────────────────────────────────────────────────
public class SpectatorMessage
{
    public int Id { get; set; }
    public string RoomId { get; set; } = "";           // Guid of PlayerRoom or TournamentMatch
    public string RoomType { get; set; } = "room";    // "room" | "match"
    public string SenderName { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsAdminBroadcast { get; set; }         // true = RCON say cũng được gửi vào game
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Donations (extended) ─────────────────────────────────────────────────────
public class Donation
{
    public int Id { get; set; }
    public string? PlayerId { get; set; }
    public string PlayerName { get; set; } = "Anonymous";
    public decimal Amount { get; set; }
    public string? Message { get; set; }
    public string? VietQRRef { get; set; }
    public string Status { get; set; } = "Pending";     // Pending/Confirmed/Rejected
    // New v1.1 fields
    public string DonationType { get; set; } = "developer"; // developer/tournament
    public string? TournamentName { get; set; }
    public string? PaymentProofPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── KYC ──────────────────────────────────────────────────────────────────────
public class KycSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerId { get; set; } = "";
    public string? CccdImagePath { get; set; }
    public string? SelfieVideoPath { get; set; }
    public string Status { get; set; } = "Pending";     // Pending/Approved/Rejected
    public string? ReviewerNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser? Player { get; set; }
}

// ── Feedback ─────────────────────────────────────────────────────────────────
public class Feedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? PlayerId { get; set; }
    public string PlayerName { get; set; } = "Anonymous";
    public string Type { get; set; } = "suggestion";   // bug/suggestion
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Game Server ───────────────────────────────────────────────────────────────
public class GameServer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";          // External IP — shown to game clients
    public int Port { get; set; } = 27015;
    public string RconPassword { get; set; } = "";
    public string? RconHost { get; set; }           // Internal RCON IP (Docker container IP) — nullable
    public string? ContainerName { get; set; }      // Docker container name — null for manual/original servers
    public string CurrentMap { get; set; } = "de_dust2";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int MaxPlayers { get; set; } = 32;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
