using Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<KillLog> KillLogs => Set<KillLog>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();
    public DbSet<PlayerRoom> PlayerRooms => Set<PlayerRoom>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<KycSubmission> KycSubmissions => Set<KycSubmission>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<GameServer> GameServers => Set<GameServer>();
    public DbSet<SpectatorMessage> SpectatorMessages => Set<SpectatorMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerStats>(e =>
        {
            e.HasKey(p => p.PlayerId);
            e.HasOne(p => p.Player)
             .WithOne(u => u.Stats)
             .HasForeignKey<PlayerStats>(p => p.PlayerId);
            e.HasIndex(p => p.EloScore);
        });

        builder.Entity<KillLog>(e =>
        {
            e.HasIndex(k => k.AttackerId);
            e.HasIndex(k => k.CreatedAt);
        });

        builder.Entity<Tournament>(e =>
        {
            e.HasIndex(t => t.Status);
        });

        builder.Entity<TournamentRegistration>(e =>
        {
            e.HasKey(r => new { r.TournamentId, r.PlayerId });
            e.HasOne(r => r.Tournament)
             .WithMany(t => t.Registrations)
             .HasForeignKey(r => r.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TournamentMatch>(e =>
        {
            e.HasOne(m => m.Tournament)
             .WithMany(t => t.Matches)
             .HasForeignKey(m => m.TournamentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.TournamentId);
            e.HasIndex(m => m.Status);
        });

        builder.Entity<PlayerRoom>(e =>
        {
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.HostPlayerId);
        });

        builder.Entity<Donation>(e =>
        {
            e.HasIndex(d => d.CreatedAt);
            e.HasIndex(d => d.VietQRRef).IsUnique().HasFilter("\"VietQRRef\" IS NOT NULL");
        });

        builder.Entity<KycSubmission>(e =>
        {
            e.HasOne(k => k.Player)
             .WithOne(u => u.Kyc)
             .HasForeignKey<KycSubmission>(k => k.PlayerId);
            e.HasIndex(k => k.PlayerId).IsUnique();
        });

        builder.Entity<Feedback>(e =>
        {
            e.HasIndex(f => f.CreatedAt);
        });

        // Seed 6 servers mặc định
        builder.Entity<GameServer>().HasData(
            new GameServer { Id = 1, Name = "🇮🇹 Italy #1",   Host = "192.168.1.2", Port = 27015, CurrentMap = "cs_italy",  Description = "Server Italy vui vẻ #1",   MaxPlayers = 32, RconPassword = "", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new GameServer { Id = 2, Name = "🇮🇹 Italy #2",   Host = "192.168.1.2", Port = 27016, CurrentMap = "cs_italy",  Description = "Server Italy vui vẻ #2",   MaxPlayers = 32, RconPassword = "", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new GameServer { Id = 3, Name = "🇮🇹 Italy #3",   Host = "192.168.1.2", Port = 27017, CurrentMap = "cs_italy",  Description = "Server Italy vui vẻ #3",   MaxPlayers = 32, RconPassword = "", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new GameServer { Id = 4, Name = "💣 Dust2",      Host = "192.168.1.2", Port = 27018, CurrentMap = "de_dust2",  Description = "Classic Dust2 deathmatch", MaxPlayers = 32, RconPassword = "", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new GameServer { Id = 5, Name = "🔥 Inferno",    Host = "192.168.1.2", Port = 27019, CurrentMap = "de_inferno",Description = "Server Inferno cạnh tranh", MaxPlayers = 32, RconPassword = "", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new GameServer { Id = 6, Name = "☢️ Nuke",       Host = "192.168.1.2", Port = 27020, CurrentMap = "de_nuke",   Description = "Server Nuke competitive",  MaxPlayers = 32, RconPassword = "", IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
