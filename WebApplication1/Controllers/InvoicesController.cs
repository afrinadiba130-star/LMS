using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status, string? search)
        {
            var query = _context.Invoices
                .Include(i => i.User)
                .Include(i => i.BorrowRecord).ThenInclude(r => r.Book)
                .AsQueryable();

            if (status == "paid")
                query = query.Where(i => i.IsPaid);
            else if (status == "unpaid")
                query = query.Where(i => !i.IsPaid);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i =>
                    i.User.FullName.Contains(search) ||
                    i.User.Email!.Contains(search) ||
                    i.BorrowRecord.Book.Title.Contains(search));

            var invoices = await query
                .OrderByDescending(i => i.IssuedDate)
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.Search = search;
            return View(invoices);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice is null)
                return NotFound();

            invoice.IsPaid = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Invoice #{id:D6} marked as PAID.";
            return RedirectToAction(nameof(Index));
        }
    }
}