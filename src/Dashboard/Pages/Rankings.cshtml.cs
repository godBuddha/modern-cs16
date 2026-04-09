using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Pages;

public class RankingsModel : PageModel
{
    private readonly AppDbContext _db;
    public RankingsModel(AppDbContext db) => _db = db;

    public List<PlayerStats> Players { get; set; } = new();
    public int TotalPlayers { get; set; }

    public async Task OnGetAsync()
    {
        TotalPlayers = await _db.PlayerStats.CountAsync();
        Players = await _db.PlayerStats
            .OrderByDescending(p => p.EloScore)
            .Take(50)
            .ToListAsync();
    }
}
