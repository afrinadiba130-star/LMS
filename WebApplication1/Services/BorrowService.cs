using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models.Entities;

namespace WebApplication1.Services
{
    public interface IBorrowService
    {
        Task<ServiceResult> BorrowBookAsync(string userId, int bookId);
        Task<ServiceResult> ReturnBookAsync(int borrowRecordId);
        Task<ServiceResult> GetMostBorrowedBooksAsync(int topCount);
        Task<ServiceResult> GetRecommendationsForUserAsync(string userId, int count = 5);
        Task<ServiceResult> GetBorrowHistoryAsync(string? userId = null);
        Task<ServiceResult> GetOverdueBorrowsAsync();
        Task<ServiceResult> GetBorrowRecordAsync(int borrowRecordId);
        Task<ServiceResult> GetDashboardStatsAsync();
    }

    public class BorrowService : IBorrowService
    {
        private const int LoanDays = 14;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BorrowService> _logger;

        public BorrowService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<BorrowService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<ServiceResult> BorrowBookAsync(string userId, int bookId)
        {
            var user = await _userManager.Users
                .Include(u => u.UserType)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return ServiceResult.Fail("User not found.");

            var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == bookId);
            if (book is null)
                return ServiceResult.Fail("Book not found.");

            if (book.AvailableCopies <= 0)
                return ServiceResult.Fail($"\"{book.Title}\" is currently unavailable. All copies are on loan.");

            var alreadyBorrowed = await _context.BorrowRecords
                .AnyAsync(r => r.UserId == userId && r.BookId == bookId && !r.IsReturned);

            if (alreadyBorrowed)
                return ServiceResult.Fail($"You already have \"{book.Title}\" on loan. Return it before borrowing again.");

            var activeBorrowCount = await _context.BorrowRecords
                .CountAsync(r => r.UserId == userId && !r.IsReturned);

            if (activeBorrowCount >= user.UserType.MaxBorrowLimit)
                return ServiceResult.Fail(
                    $"Borrow limit reached for {user.UserType.TypeName} (max {user.UserType.MaxBorrowLimit} books). " +
                    "Return a book before borrowing another.");

            var record = new BorrowRecord
            {
                UserId = userId,
                BookId = bookId,
                BorrowDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(LoanDays),
                IsReturned = false
            };

            book.AvailableCopies -= 1;
            _context.BorrowRecords.Add(record);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} borrowed book {BookId}, due {DueDate}", userId, bookId, record.DueDate);
            return ServiceResult.Ok($"\"{book.Title}\" issued successfully. Due date: {record.DueDate:dd MMM yyyy}.", record.Id);
        }

        public async Task<ServiceResult> ReturnBookAsync(int borrowRecordId)
        {
            var record = await _context.BorrowRecords
                .Include(r => r.Book)
                .Include(r => r.User)
                .ThenInclude(u => u!.UserType)
                .FirstOrDefaultAsync(r => r.Id == borrowRecordId);

            if (record is null)
                return ServiceResult.Fail("Borrow record not found.");

            if (record.IsReturned)
                return ServiceResult.Fail("This book has already been returned.");

            record.ReturnDate = DateTime.Today;
            record.IsReturned = true;

            var overdueDays = (record.ReturnDate.Value.Date - record.DueDate.Date).Days;
            var fineRate = record.User.UserType.FineRatePerDay;
            record.FineAmount = overdueDays > 0 ? overdueDays * fineRate : 0;

            record.Book.AvailableCopies += 1;

            if (record.FineAmount > 0)
            {
                _context.Invoices.Add(new Invoice
                {
                    BorrowRecordId = record.Id,
                    UserId = record.UserId,
                    TotalFine = record.FineAmount,
                    IssuedDate = DateTime.Now,
                    IsPaid = false
                });
            }

            await _context.SaveChangesAsync();

            var message = record.FineAmount > 0
                ? $"Book returned. Fine: Tk {record.FineAmount:N2} (invoice generated)."
                : "Book returned on time. No fine charged.";

            _logger.LogInformation("Book {BookId} returned by {UserId}, fine {Fine}", record.BookId, record.UserId, record.FineAmount);
            return ServiceResult.Ok(message, record.Id);
        }

        public async Task<ServiceResult> GetMostBorrowedBooksAsync(int topCount)
        {
            var mostBorrowed = await _context.BorrowRecords
                .Where(r => r.IsReturned)
                .GroupBy(r => new { r.BookId, r.Book.Title, r.Book.Author, r.Book.Genre })
                .Select(g => new MostBorrowedBookDto
                {
                    BookId = g.Key.BookId,
                    Title = g.Key.Title,
                    Author = g.Key.Author,
                    Genre = g.Key.Genre,
                    BorrowCount = g.Count(),
                    AvailableCopies = _context.Books.FirstOrDefault(b => b.Id == g.Key.BookId)!.AvailableCopies
                })
                .OrderByDescending(x => x.BorrowCount)
                .Take(topCount)
                .ToListAsync();

            return ServiceResult.Ok(data: mostBorrowed);
        }

        public async Task<ServiceResult> GetRecommendationsForUserAsync(string userId, int count = 5)
        {
            var user = await _userManager.Users.Include(u => u.UserType)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return ServiceResult.Fail("User not found.");

            var favoriteGenres = await _context.BorrowRecords
                .Where(r => r.UserId == userId)
                .Select(r => r.Book.Genre)
                .Distinct()
                .ToListAsync();

            if (favoriteGenres.Count == 0)
            {
                var popular = await _context.Books
                    .Where(b => b.AvailableCopies > 0)
                    .OrderBy(b => b.Title)
                    .Take(count)
                    .Select(b => new RecommendedBookDto
                    {
                        BookId = b.Id,
                        Title = b.Title,
                        Author = b.Author,
                        Genre = b.Genre,
                        Reason = "Popular pick — we suggest starting with this one"
                    })
                    .ToListAsync();

                return ServiceResult.Ok(data: popular);
            }

            var borrowedBookIds = await _context.BorrowRecords
                .Where(r => r.UserId == userId)
                .Select(r => r.BookId)
                .Distinct()
                .ToListAsync();

            var recommendations = await _context.BorrowRecords
                .Where(r => favoriteGenres.Contains(r.Book.Genre)
                            && !borrowedBookIds.Contains(r.BookId)
                            && r.Book.AvailableCopies > 0)
                .GroupBy(r => new { r.Book.Id, r.Book.Title, r.Book.Author, r.Book.Genre })
                .Select(g => new RecommendedBookDto
                {
                    BookId = g.Key.Id,
                    Title = g.Key.Title,
                    Author = g.Key.Author,
                    Genre = g.Key.Genre,
                    Reason = $"Because you like {g.Key.Genre}"
                })
                .OrderByDescending(x => x.BookId)
                .Take(count)
                .ToListAsync();

            return ServiceResult.Ok(data: recommendations);
        }

        public async Task<ServiceResult> GetBorrowHistoryAsync(string? userId = null)
        {
            var query = _context.BorrowRecords
                .Include(r => r.Book)
                .Include(r => r.User)
                .ThenInclude(u => u!.UserType)
                .Include(r => r.Invoice)
                .AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(r => r.UserId == userId);

            var records = await query
                .OrderByDescending(r => r.BorrowDate)
                .ToListAsync();

            return ServiceResult.Ok(data: records);
        }

        public async Task<ServiceResult> GetOverdueBorrowsAsync()
        {
            var overdue = await _context.BorrowRecords
                .Include(r => r.Book)
                .Include(r => r.User)
                .Where(r => !r.IsReturned && r.DueDate < DateTime.Today)
                .OrderBy(r => r.DueDate)
                .ToListAsync();

            return ServiceResult.Ok(data: overdue);
        }

        public async Task<ServiceResult> GetBorrowRecordAsync(int borrowRecordId)
        {
            var record = await _context.BorrowRecords
                .Include(r => r.Book)
                .Include(r => r.User)
                .ThenInclude(u => u!.UserType)
                .Include(r => r.Invoice)
                .FirstOrDefaultAsync(r => r.Id == borrowRecordId);

            if (record is null)
                return ServiceResult.Fail("Borrow record not found.");

            return ServiceResult.Ok(data: record);
        }

        public async Task<ServiceResult> GetDashboardStatsAsync()
        {
            var stats = new DashboardStatsDto
            {
                TotalBooks = await _context.Books.CountAsync(),
                TotalCopies = await _context.Books.SumAsync(b => b.TotalCopies),
                ActiveBorrows = await _context.BorrowRecords.CountAsync(r => !r.IsReturned),
                OverdueBorrows = await _context.BorrowRecords.CountAsync(r => !r.IsReturned && r.DueDate < DateTime.Today),
                UnpaidFines = await _context.Invoices
                    .Where(i => !i.IsPaid)
                    .SumAsync(i => (decimal?)i.TotalFine) ?? 0
            };

            return ServiceResult.Ok(data: stats);
        }
    }

    public class MostBorrowedBookDto
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public int BorrowCount { get; set; }
        public int AvailableCopies { get; set; }
    }

    public class RecommendedBookDto
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class DashboardStatsDto
    {
        public int TotalBooks { get; set; }
        public int TotalCopies { get; set; }
        public int ActiveBorrows { get; set; }
        public int OverdueBorrows { get; set; }
        public decimal UnpaidFines { get; set; }
    }
}
