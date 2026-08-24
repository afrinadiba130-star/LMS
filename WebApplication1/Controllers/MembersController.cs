using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MembersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MembersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));

            var members = await query
                .Include(u => u.UserType)
                .OrderBy(u => u.FullName)
                .Select(u => new MemberListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    TypeName = u.UserType.TypeName,
                    ActiveBorrows = u.BorrowRecords.Count(r => !r.IsReturned),
                    UnpaidFines = u.BorrowRecords.Sum(r => r.Invoice != null && !r.Invoice.IsPaid ? r.Invoice.TotalFine : 0)
                })
                .ToListAsync();

            ViewBag.Search = search;
            return View(members);
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _context.Users
                .Include(u => u.UserType)
                .Include(u => u.BorrowRecords).ThenInclude(r => r.Book)
                .Include(u => u.BorrowRecords).ThenInclude(r => r.Invoice)
                .Include(u => u.Payments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user is null)
                return NotFound();

            var vm = new MemberDetailsViewModel
            {
                Member = user,
                UserTypes = await _context.UserTypes
                    .OrderBy(t => t.Id)
                    .Select(t => new SelectListItem
                    {
                        Value = t.Id.ToString(),
                        Text = $"{t.TypeName} — max {t.MaxBorrowLimit} books, fine Tk {t.FineRatePerDay}/day"
                    })
                    .ToListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeType(string userId, int userTypeId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user is null)
                return NotFound();

            var userType = await _context.UserTypes.FindAsync(userTypeId);
            if (userType is null)
            {
                TempData["Error"] = "Invalid member type selected.";
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            var activeBorrows = await _context.BorrowRecords
                .CountAsync(r => r.UserId == userId && !r.IsReturned);

            if (activeBorrows > userType.MaxBorrowLimit)
            {
                TempData["Error"] = $"Cannot assign {userType.TypeName}: the member currently has {activeBorrows} active borrows, but this type allows only {userType.MaxBorrowLimit}.";
                return RedirectToAction(nameof(Details), new { id = userId });
            }

            user.UserTypeId = userTypeId;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Member type updated to {userType.TypeName}.";
            return RedirectToAction(nameof(Details), new { id = userId });
        }
    }
}