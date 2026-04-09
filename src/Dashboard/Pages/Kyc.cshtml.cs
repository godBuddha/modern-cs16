using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Pages;

public class KycModel : PageModel
{
    private readonly AppDbContext _db;
    public KycModel(AppDbContext db) => _db = db;

    public List<KycSubmission> Queue { get; set; } = new();

    public async Task OnGetAsync()
    {
        Queue = await _db.KycSubmissions.Where(k => k.Status == "Pending")
            .OrderBy(k => k.SubmittedAt).ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var kyc = await _db.KycSubmissions.FindAsync(id);
        if (kyc != null) { kyc.Status = "Approved"; kyc.ReviewedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? note)
    {
        var kyc = await _db.KycSubmissions.FindAsync(id);
        if (kyc != null) { kyc.Status = "Rejected"; kyc.ReviewerNote = note; kyc.ReviewedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return RedirectToPage();
    }
}
