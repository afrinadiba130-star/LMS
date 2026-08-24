using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfReportService _pdfReportService;

        public PaymentsController(ApplicationDbContext context, IPdfReportService pdfReportService)
        {
            _context = context;
            _pdfReportService = pdfReportService;
        }

        public async Task<IActionResult> Index(string? type)
        {
            var query = _context.Payments
                .Include(p => p.User)
                .Include(p => p.User.UserType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) &&
                (type == AppConstants.MembershipPayment || type == AppConstants.FinePayment))
            {
                query = query.Where(p => p.PaymentType == type);
            }

            var payments = await query
                .OrderByDescending(p => p.PaidDate)
                .ToListAsync();

            ViewBag.SelectedType = type;
            ViewBag.TotalAmount = payments.Sum(p => p.Amount);
            return View(payments);
        }

        public async Task<IActionResult> DownloadReceipt(int id)
        {
            var result = await _pdfReportService.GeneratePaymentReceiptPdfAsync(id);
            if (!result.Success || result.Data is not byte[] pdf)
                return NotFound();

            return File(pdf, "application/pdf", $"payment-receipt-{id}.pdf");
        }

        public async Task<IActionResult> DownloadReport(string? type, int year, int month)
        {
            if (year < 2000 || year > 2100 || month < 1 || month > 12)
                return BadRequest();

            var result = await _pdfReportService.GeneratePaymentsReportPdfAsync(type, year, month);
            if (!result.Success || result.Data is not byte[] pdf)
                return NotFound();

            return File(pdf, "application/pdf", $"payments-report-{year}-{month:D2}.pdf");
        }
    }
}