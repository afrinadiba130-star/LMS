using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IBorrowService _borrowService;
        private readonly ApplicationDbContext _context;

        public AdminController(IBorrowService borrowService, ApplicationDbContext context)
        {
            _borrowService = borrowService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var statsResult = await _borrowService.GetDashboardStatsAsync();
            var stats = (DashboardStatsDto?)statsResult.Data;

            var mostBorrowedResult = await _borrowService.GetMostBorrowedBooksAsync(5);
            var overdueResult = await _borrowService.GetOverdueBorrowsAsync();

            ViewBag.Stats = stats ?? new DashboardStatsDto();
            ViewBag.MostBorrowed = mostBorrowedResult.Data as List<MostBorrowedBookDto> ?? new();
            ViewBag.OverdueRecords = overdueResult.Data as List<Models.Entities.BorrowRecord> ?? new();

            ViewBag.MemberCount = await _context.Users.CountAsync();
            ViewBag.InvoiceCount = await _context.Invoices.CountAsync();
            ViewBag.UnpaidInvoiceCount = await _context.Invoices.CountAsync(i => !i.IsPaid);
            ViewBag.UserTypes = await _context.UserTypes.OrderBy(t => t.Id).ToListAsync();

            return View();
        }
    }
}