using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models.Entities;
using WebApplication1.Services;
using WebApplication1.ViewModels;

namespace WebApplication1.Controllers
{
    public class BorrowController : Controller
    {
        private readonly IBorrowService _borrowService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public BorrowController(
            IBorrowService borrowService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _borrowService = borrowService;
            _userManager = userManager;
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin");
            var userId = isAdmin ? null : user!.Id;

            var result = await _borrowService.GetBorrowHistoryAsync(userId);
            return View(result.Data as List<BorrowRecord> ?? new());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ReturnOwn(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Challenge();

            var record = await _context.BorrowRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (record is null)
                return NotFound();

            var result = await _borrowService.ReturnBookAsync(id);

            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(History));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> PayFine(int invoiceId, string? trxId, string? senderNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Challenge();

            var invoice = await _context.Invoices
                .Include(i => i.BorrowRecord)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice is null)
                return NotFound();

            if (invoice.UserId != user.Id && !User.IsInRole("Admin"))
                return Forbid();

            if (invoice.IsPaid)
            {
                TempData["Error"] = "This invoice has already been paid.";
                return RedirectToAction(nameof(History));
            }

            if (string.IsNullOrWhiteSpace(trxId) || string.IsNullOrWhiteSpace(senderNumber))
            {
                TempData["Error"] = "Please enter the bKash TrxID and the sender number.";
                return RedirectToAction(nameof(History));
            }

            _context.Payments.Add(new Payment
            {
                UserId = invoice.UserId,
                InvoiceId = invoice.Id,
                PaymentType = AppConstants.FinePayment,
                Amount = invoice.TotalFine,
                BkashNumber = AppConstants.BkashNumber,
                BkashTrxId = trxId.Trim(),
                SenderNumber = senderNumber.Trim(),
                PaidDate = DateTime.Now,
                IsVerified = true
            });

            invoice.IsPaid = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Fine of Tk {invoice.TotalFine:N2} paid via bKash (TrxID {trxId.Trim()}). Invoice settled.";
            return RedirectToAction(nameof(History));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Issue()
        {
            var vm = new BorrowIssueViewModel
            {
                Users = await GetUserOptionsAsync(),
                Books = await GetAvailableBookOptionsAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Issue(BorrowIssueViewModel vm)
        {
            var result = await _borrowService.BorrowBookAsync(vm.UserId, vm.BookId);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(History));
            }

            TempData["Error"] = result.Message;

            vm.Users = await GetUserOptionsAsync();
            vm.Books = await GetAvailableBookOptionsAsync();
            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Return(int? id = null)
        {
            var vm = new ReturnViewModel
            {
                BorrowRecords = await GetActiveBorrowOptionsAsync()
            };

            if (id.HasValue)
            {
                var recordResult = await _borrowService.GetBorrowRecordAsync(id.Value);
                if (recordResult.Success && recordResult.Data is BorrowRecord record)
                    ApplyRecordToViewModel(vm, record);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Return(ReturnViewModel vm)
        {
            var result = await _borrowService.ReturnBookAsync(vm.BorrowRecordId);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(History));
            }

            TempData["Error"] = result.Message;
            vm.BorrowRecords = await GetActiveBorrowOptionsAsync();
            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PreviewReturn(int id)
        {
            var result = await _borrowService.GetBorrowRecordAsync(id);
            if (!result.Success || result.Data is not BorrowRecord record)
                return NotFound();

            var vm = new ReturnViewModel { BorrowRecordId = record.Id };
            ApplyRecordToViewModel(vm, record);
            return PartialView("_ReturnPreview", vm);
        }

        private static void ApplyRecordToViewModel(ReturnViewModel vm, BorrowRecord record)
        {
            var overdueDays = (DateTime.Today.Date - record.DueDate.Date).Days;

            vm.BookTitle = record.Book.Title;
            vm.MemberName = record.User.FullName;
            vm.MemberType = record.User.UserType.TypeName;
            vm.BorrowDate = record.BorrowDate;
            vm.DueDate = record.DueDate;
            vm.FineRatePerDay = record.User.UserType.FineRatePerDay;
            vm.OverdueDays = overdueDays > 0 ? overdueDays : 0;
            vm.ProjectedFine = vm.OverdueDays * vm.FineRatePerDay;
        }

        private async Task<List<SelectListItem>> GetUserOptionsAsync()
        {
            return await _context.Users
                .Include(u => u.UserType)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.UserType.TypeName})"
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetAvailableBookOptionsAsync()
        {
            return await _context.Books
                .Where(b => b.AvailableCopies > 0)
                .OrderBy(b => b.Title)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"{b.Title} — {b.Author} ({b.AvailableCopies} available)"
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetActiveBorrowOptionsAsync()
        {
            return await _context.BorrowRecords
                .Include(r => r.Book)
                .Include(r => r.User)
                .Where(r => !r.IsReturned)
                .OrderByDescending(r => r.BorrowDate)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = $"{r.Book.Title} — {r.User.FullName} (due {r.DueDate:dd MMM yyyy})"
                })
                .ToListAsync();
        }
    }
}
