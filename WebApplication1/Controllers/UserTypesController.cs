using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models.Entities;
using WebApplication1.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserTypesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var types = await _context.UserTypes
                .OrderBy(t => t.Id)
                .ToListAsync();

            return View(types);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var type = await _context.UserTypes.FindAsync(id);
            if (type is null)
                return NotFound();

            var vm = new UserTypeEditViewModel
            {
                Id = type.Id,
                TypeName = type.TypeName,
                MaxBorrowLimit = type.MaxBorrowLimit,
                FineRatePerDay = type.FineRatePerDay,
                MonthlyFee = type.MonthlyFee,
                Description = type.Description
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserTypeEditViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var type = await _context.UserTypes.FindAsync(vm.Id);
            if (type is null)
                return NotFound();

            var maxActive = await _context.BorrowRecords
                .Where(r => !r.IsReturned)
                .GroupBy(r => r.UserId)
                .Select(g => g.Count())
                .DefaultIfEmpty()
                .MaxAsync();

            if (vm.MaxBorrowLimit < maxActive)
            {
                ModelState.AddModelError(nameof(vm.MaxBorrowLimit),
                    $"Cannot set limit below {maxActive} — a member currently has that many active borrows.");
                return View(vm);
            }

            type.MaxBorrowLimit = vm.MaxBorrowLimit;
            type.FineRatePerDay = vm.FineRatePerDay;
            type.MonthlyFee = vm.MonthlyFee;
            type.Description = vm.Description;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{type.TypeName} rules updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}