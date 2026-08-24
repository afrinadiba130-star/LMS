using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Services;
using WebApplication1.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly IPdfReportService _pdfService;
        private readonly ApplicationDbContext _context;

        public ReportsController(IPdfReportService pdfService, ApplicationDbContext context)
        {
            _pdfService = pdfService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new ReportsIndexViewModel
            {
                UnpaidInvoices = await _context.Invoices
                    .Include(i => i.User)
                    .Include(i => i.BorrowRecord).ThenInclude(r => r.Book)
                    .Where(i => !i.IsPaid)
                    .OrderByDescending(i => i.IssuedDate)
                    .Take(20)
                    .ToListAsync(),

                RecentInvoices = await _context.Invoices
                    .Include(i => i.User)
                    .Include(i => i.BorrowRecord).ThenInclude(r => r.Book)
                    .OrderByDescending(i => i.IssuedDate)
                    .Take(10)
                    .ToListAsync()
            };

            ViewBag.CurrentMonth = DateTime.Today.Month;
            ViewBag.CurrentYear = DateTime.Today.Year;

            return View(vm);
        }

        public async Task<IActionResult> DownloadInvoicePdf(int id)
        {
            var result = await _pdfService.GenerateFineInvoicePdfAsync(id);

            if (!result.Success || result.Data is not byte[] pdf)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            return File(pdf, "application/pdf", $"FineInvoice_{id:D6}.pdf");
        }

        public async Task<IActionResult> DownloadMonthlyPdf(int year, int month)
        {
            if (year < 2000 || year > 2100 || month < 1 || month > 12)
            {
                TempData["Error"] = "Invalid month or year.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _pdfService.GenerateMonthlyReportPdfAsync(year, month);

            if (!result.Success || result.Data is not byte[] pdf)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            return File(pdf, "application/pdf", $"MonthlyReport_{year:D4}_{month:D2}.pdf");
        }

        public async Task<IActionResult> DownloadMostBorrowedPdf(int topCount = 10)
        {
            if (topCount < 1 || topCount > 50)
                topCount = 10;

            var result = await _pdfService.GenerateMostBorrowedBooksPdfAsync(topCount);

            if (!result.Success || result.Data is not byte[] pdf)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            return File(pdf, "application/pdf", $"MostBorrowedBooks_Top{topCount}.pdf");
        }
    }
}
