using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models.Entities;
using WebApplication1.ViewModels;

namespace WebApplication1.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var vm = new RegisterViewModel
            {
                UserTypes = await GetUserTypeOptionsAsync()
            };
            ViewBag.MemberTypes = await _context.UserTypes.OrderBy(t => t.Id).ToListAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.UserTypes = await GetUserTypeOptionsAsync();
                ViewBag.MemberTypes = await _context.UserTypes.OrderBy(t => t.Id).ToListAsync();
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(vm.BkashTrxId) || string.IsNullOrWhiteSpace(vm.BkashSenderNumber))
            {
                ModelState.AddModelError(string.Empty, "Please complete the bKash payment info (TrxID and sender number).");
                vm.UserTypes = await GetUserTypeOptionsAsync();
                ViewBag.MemberTypes = await _context.UserTypes.OrderBy(t => t.Id).ToListAsync();
                return View(vm);
            }

            var userType = await _context.UserTypes.FindAsync(vm.UserTypeId);
            if (userType is null)
            {
                ModelState.AddModelError(nameof(vm.UserTypeId), "Select a valid member type.");
                vm.UserTypes = await GetUserTypeOptionsAsync();
                ViewBag.MemberTypes = await _context.UserTypes.OrderBy(t => t.Id).ToListAsync();
                return View(vm);
            }

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                FullName = vm.FullName,
                UserTypeId = userType.Id
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                vm.UserTypes = await GetUserTypeOptionsAsync();
                ViewBag.MemberTypes = await _context.UserTypes.OrderBy(t => t.Id).ToListAsync();
                return View(vm);
            }

            _context.Payments.Add(new Payment
            {
                UserId = user.Id,
                PaymentType = AppConstants.MembershipPayment,
                Amount = userType.MonthlyFee,
                BkashNumber = AppConstants.BkashNumber,
                BkashTrxId = vm.BkashTrxId.Trim(),
                SenderNumber = vm.BkashSenderNumber.Trim(),
                PaidDate = DateTime.Now,
                IsVerified = true
            });

            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Success"] = $"Registration successful! Membership fee of Tk {userType.MonthlyFee:N2} recorded (bKash TrxID {vm.BkashTrxId}). Welcome!";
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var result = await _signInManager.PasswordSignInAsync(
                vm.Email, vm.Password, vm.RememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(vm);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetUserTypeOptionsAsync()
        {
            var types = await _context.UserTypes
                .OrderBy(t => t.Id)
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = $"{t.TypeName} — {t.MaxBorrowLimit} books, fine Tk {t.FineRatePerDay}/day, fee Tk {t.MonthlyFee}/month"
                })
                .ToListAsync();

            return types;
        }
    }
}
