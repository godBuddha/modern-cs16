using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Pages;

public class TournamentsModel : PageModel
{
    private readonly AppDbContext _db;

    public TournamentsModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Tournament> Tournaments { get; set; } = new();
    public Dictionary<Guid, int> RegCounts { get; set; } = new();

    public async Task OnGetAsync()
    {
        Tournaments = await _db.Tournaments.OrderByDescending(t => t.CreatedAt).ToListAsync();
        
        var regs = await _db.TournamentRegistrations.GroupBy(r => r.TournamentId)
                            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        foreach (var r in regs) RegCounts[r.Key] = r.Count;
    }

    public async Task<IActionResult> OnPostCreateAsync(string title, decimal entryFee, decimal prizePool, int maxPlayers)
    {
        if (!string.IsNullOrEmpty(title))
        {
            _db.Tournaments.Add(new Tournament
            {
                Title = title,
                EntryFee = entryFee,
                PrizePool = prizePool,
                MaxPlayers = maxPlayers > 0 ? maxPlayers : 16,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(Guid id, string status)
    {
        var t = await _db.Tournaments.FindAsync(id);
        if (t != null)
        {
            t.Status = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
