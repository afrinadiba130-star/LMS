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
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBorrowService _borrowService;
        private readonly UserManager<ApplicationUser> _userManager;

        public BooksController(
            ApplicationDbContext context,
            IBorrowService borrowService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _borrowService = borrowService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, string? genre)
        {
            var query = _context.Books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(b =>
                    b.Title.Contains(search) ||
                    b.Author.Contains(search) ||
                    b.ISBN.Contains(search));

            if (!string.IsNullOrWhiteSpace(genre))
                query = query.Where(b => b.Genre == genre);

            var books = await query
                .OrderBy(b => b.Title)
                .ToListAsync();

            var genres = await _context.Books
                .Select(b => b.Genre)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Genre = genre;
            ViewBag.Genres = genres;

            var userId = _userManager.GetUserId(User);
            var user = userId is not null
                ? await _context.Users.Include(u => u.UserType).FirstOrDefaultAsync(u => u.Id == userId)
                : null;
            ViewBag.MyActiveBorrows = new HashSet<int>();
            ViewBag.MyBorrowCount = 0;
            ViewBag.MyBorrowLimit = 0;

            if (user is not null)
            {
                var activeBorrows = await _context.BorrowRecords
                    .Where(r => r.UserId == user.Id && !r.IsReturned)
                    .Select(r => r.BookId)
                    .ToListAsync();

                ViewBag.MyActiveBorrows = activeBorrows.ToHashSet();
                ViewBag.MyBorrowCount = activeBorrows.Count;
                ViewBag.MyBorrowLimit = user.UserType.MaxBorrowLimit;
            }

            return View(books);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Borrow(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Challenge();

            var result = await _borrowService.BorrowBookAsync(user.Id, id);

            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new BookFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(BookFormViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            if (await _context.Books.AnyAsync(b => b.ISBN == vm.ISBN))
            {
                ModelState.AddModelError(nameof(vm.ISBN), "A book with this ISBN already exists.");
                return View(vm);
            }

            var book = new Book
            {
                Title = vm.Title,
                Author = vm.Author,
                ISBN = vm.ISBN,
                Genre = vm.Genre,
                Language = vm.Language ?? "Bengali",
                Description = vm.Description,
                TotalCopies = vm.TotalCopies,
                AvailableCopies = vm.TotalCopies
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{book.Title}\" added to the catalog.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book is null)
                return NotFound();

            var vm = new BookFormViewModel
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                Genre = book.Genre,
                Language = book.Language,
                Description = book.Description,
                TotalCopies = book.TotalCopies
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(BookFormViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var book = await _context.Books.FindAsync(vm.Id);
            if (book is null)
                return NotFound();

            var isbnTaken = await _context.Books.AnyAsync(b => b.ISBN == vm.ISBN && b.Id != vm.Id);
            if (isbnTaken)
            {
                ModelState.AddModelError(nameof(vm.ISBN), "A book with this ISBN already exists.");
                return View(vm);
            }

            var copiesChange = vm.TotalCopies - book.TotalCopies;
            book.Title = vm.Title;
            book.Author = vm.Author;
            book.ISBN = vm.ISBN;
            book.Genre = vm.Genre;
            book.Language = vm.Language ?? book.Language;
            book.Description = vm.Description;
            book.TotalCopies = vm.TotalCopies;
            book.AvailableCopies = Math.Max(0, book.AvailableCopies + copiesChange);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Book updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book is null)
                return NotFound();

            var hasBorrows = await _context.BorrowRecords.AnyAsync(r => r.BookId == id);
            if (hasBorrows)
            {
                TempData["Error"] = $"\"{book.Title}\" cannot be deleted because it has borrow history.";
                return RedirectToAction(nameof(Index));
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"\"{book.Title}\" deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
