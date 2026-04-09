using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public int TotalPlayers { get; set; }
    public int OpenTournaments { get; set; }
    public decimal TotalDonations { get; set; }
    public int PendingKyc { get; set; }
    public List<PlayerStats> TopPlayers { get; set; } = new();
    public List<Donation> RecentDonations { get; set; } = new();

    public async Task OnGetAsync()
    {
        TotalPlayers = await _db.PlayerStats.CountAsync();
        OpenTournaments = await _db.Tournaments.CountAsync(t => t.Status == "Open");
        TotalDonations = await _db.Donations.Where(d => d.Status == "Confirmed").SumAsync(d => (decimal?)d.Amount) ?? 0;
        PendingKyc = await _db.KycSubmissions.CountAsync(k => k.Status == "Pending");
        TopPlayers = await _db.PlayerStats.OrderByDescending(p => p.EloScore).Take(5).ToListAsync();
        RecentDonations = await _db.Donations.OrderByDescending(d => d.CreatedAt).Take(5).ToListAsync();
    }
}
