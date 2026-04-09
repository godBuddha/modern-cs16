using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Pages;

public class DonationsModel : PageModel
{
    private readonly AppDbContext _db;
    public DonationsModel(AppDbContext db) => _db = db;

    public List<Donation> Donations { get; set; } = new();
    public decimal TotalConfirmed { get; set; }
    public int PendingCount { get; set; }
    public int TotalCount { get; set; }
    public string StatusFilter { get; set; } = "";

    public async Task OnGetAsync(string? status = null)
    {
        StatusFilter = status ?? "";
        var query = _db.Donations.AsQueryable();
        if (!string.IsNullOrEmpty(StatusFilter))
            query = query.Where(d => d.Status == StatusFilter);

        Donations = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        TotalConfirmed = await _db.Donations.Where(d => d.Status == "Confirmed").SumAsync(d => (decimal?)d.Amount) ?? 0;
        PendingCount = await _db.Donations.CountAsync(d => d.Status == "Pending");
        TotalCount = await _db.Donations.CountAsync();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation != null)
        {
            donation.Status = "Confirmed";
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation != null)
        {
            donation.Status = "Rejected";
            await _db.SaveChangesAsync();
        }
        return RedirectToPage();
    }
}
