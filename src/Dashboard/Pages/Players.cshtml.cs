using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Pages;

public class PlayersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public PlayersModel(AppDbContext db, IHttpClientFactory hf, IConfiguration cfg)
    {
        _db = db; _http = hf.CreateClient(); _config = cfg;
    }

    public List<PlayerStats> Players { get; set; } = new();
    public Dictionary<string, string> KycMap { get; set; } = new();

    public async Task OnGetAsync()
    {
        Players = await _db.PlayerStats.OrderByDescending(p => p.EloScore).ToListAsync();
        KycMap = await _db.KycSubmissions.ToDictionaryAsync(k => k.PlayerId, k => k.Status);
    }

    public async Task<IActionResult> OnPostToggleBanAsync(string playerId, bool banned)
    {
        var stats = await _db.PlayerStats.FindAsync(playerId);
        if (stats != null)
        {
            stats.IsBanned = banned;
            stats.BanReason = banned ? "Admin action" : null;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
