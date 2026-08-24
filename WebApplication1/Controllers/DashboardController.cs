using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models.Entities;
using WebApplication1.Services;
using WebApplication1.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IBorrowService _borrowService;
        private readonly IPdfReportService _pdfReportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DashboardController(
            IBorrowService borrowService,
            IPdfReportService pdfReportService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _borrowService = borrowService;
            _pdfReportService = pdfReportService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var statsResult = await _borrowService.GetDashboardStatsAsync();
            var stats = (DashboardStatsDto?)statsResult.Data;

            var mostBorrowedResult = await _borrowService.GetMostBorrowedBooksAsync(5);
            var recommendationsResult = await _borrowService.GetRecommendationsForUserAsync(user!.Id, 5);
            var overdueResult = await _borrowService.GetOverdueBorrowsAsync();

            var myActiveBorrows = await _context.BorrowRecords
                .Where(r => r.UserId == user.Id && !r.IsReturned)
                .CountAsync();

            var myPayments = await _context.Payments
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.PaidDate)
                .Select(p => new MyPaymentDto
                {
                    PaymentId = p.Id,
                    PaymentType = p.PaymentType,
                    Amount = p.Amount,
                    BkashTrxId = p.BkashTrxId,
                    PaidDate = p.PaidDate,
                    InvoiceId = p.InvoiceId
                })
                .ToListAsync();

            var vm = new DashboardViewModel
            {
                TotalBooks = stats?.TotalBooks ?? 0,
                TotalCopies = stats?.TotalCopies ?? 0,
                ActiveBorrows = stats?.ActiveBorrows ?? 0,
                OverdueBorrows = stats?.OverdueBorrows ?? 0,
                UnpaidFines = stats?.UnpaidFines ?? 0,
                MostBorrowedBooks = mostBorrowedResult.Data as List<MostBorrowedBookDto> ?? new(),
                Recommendations = recommendationsResult.Data as List<RecommendedBookDto> ?? new(),
                OverdueRecords = overdueResult.Data as List<BorrowRecord> ?? new(),
                MyBorrowLimit = user.UserType.MaxBorrowLimit,
                MyActiveBorrows = myActiveBorrows,
                MyPayments = myPayments
            };

            return View(vm);
        }

        public async Task<IActionResult> DownloadPaymentReceipt(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Challenge();

            var payment = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment is null)
                return NotFound();

            if (payment.UserId != user.Id && !User.IsInRole("Admin"))
                return Forbid();

            var result = await _pdfReportService.GeneratePaymentReceiptPdfAsync(id);
            if (!result.Success || result.Data is not byte[] pdf)
                return NotFound();

            return File(pdf, "application/pdf", $"payment-receipt-{id}.pdf");
        }
    }
}
