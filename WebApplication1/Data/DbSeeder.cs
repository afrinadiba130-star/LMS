using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Entities;

namespace WebApplication1.Data
{
    public class DbSeeder : IDbSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DbSeeder> _logger;

        public DbSeeder(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env,
            ILogger<DbSeeder> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _env = env;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            await SeedUserTypesAsync();
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedBooksAsync();
        }

        private async Task SeedUserTypesAsync()
        {
            var types = new[]
            {
                new UserType { TypeName = "Regular", MaxBorrowLimit = 2, FineRatePerDay = 20, MonthlyFee = 100, Description = "Max 2 books, fine Tk 20/day, Tk 100/month" },
                new UserType { TypeName = "Pro Member", MaxBorrowLimit = 4, FineRatePerDay = 10, MonthlyFee = 150, Description = "Max 4 books, fine Tk 10/day, Tk 150/month" },
                new UserType { TypeName = "Premium", MaxBorrowLimit = 8, FineRatePerDay = 0, MonthlyFee = 250, Description = "Max 8 books, no fines, Tk 250/month" }
            };

            foreach (var type in types)
            {
                var existing = await _context.UserTypes.FirstOrDefaultAsync(t => t.TypeName == type.TypeName);
                if (existing is null)
                {
                    _context.UserTypes.Add(type);
                }
                else
                {
                    existing.MaxBorrowLimit = type.MaxBorrowLimit;
                    existing.FineRatePerDay = type.FineRatePerDay;
                    existing.MonthlyFee = type.MonthlyFee;
                    existing.Description = type.Description;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded user types: Regular, Pro Member, Premium (fees Tk 100/150/250 per month)");
        }

        private async Task SeedRolesAsync()
        {
            const string adminRole = "Admin";

            if (!await _roleManager.RoleExistsAsync(adminRole))
                await _roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        private async Task SeedUsersAsync()
        {
            var premiumType = await _context.UserTypes.FirstAsync(u => u.TypeName == "Premium");
            var regularType = await _context.UserTypes.FirstAsync(u => u.TypeName == "Regular");

            const string adminEmail = "afrinadiba15@gmail.com";
            if (await _userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "Afrin Adiba",
                    UserTypeId = premiumType.Id
                };

                var createResult = await _userManager.CreateAsync(admin, "Adib@123");
                if (createResult.Succeeded)
                {
                    await _userManager.AddToRoleAsync(admin, "Admin");
                    _logger.LogInformation("Seeded admin user: {Email}", adminEmail);
                }
            }

            var legacyAdmin = await _userManager.FindByEmailAsync("admin@library.com");
            if (legacyAdmin is not null && await _userManager.IsInRoleAsync(legacyAdmin, "Admin"))
            {
                await _userManager.RemoveFromRoleAsync(legacyAdmin, "Admin");
                _logger.LogInformation("Removed legacy admin role from admin@library.com");
            }

            if (await _userManager.FindByEmailAsync("user@library.com") == null)
            {
                var regular = new ApplicationUser
                {
                    UserName = "user@library.com",
                    Email = "user@library.com",
                    EmailConfirmed = true,
                    FullName = "Demo Regular User",
                    UserTypeId = regularType.Id
                };

                var createRegular = await _userManager.CreateAsync(regular, "User@123");
                if (createRegular.Succeeded)
                    _logger.LogInformation("Seeded demo user: user@library.com");
            }
        }

        private async Task SeedBooksAsync()
        {
            if (await _context.Books.AnyAsync())
                return;

            var jsonPath = Path.Combine(_env.ContentRootPath, "Data", "SeedData", "books.json");
            var json = await File.ReadAllTextAsync(jsonPath);
            var books = JsonSerializer.Deserialize<List<BookSeedDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<BookSeedDto>();

            foreach (var dto in books)
            {
                _context.Books.Add(new Book
                {
                    Title = dto.Title,
                    Author = dto.Author,
                    ISBN = dto.Isbn,
                    Genre = dto.Genre,
                    Language = dto.Language,
                    TotalCopies = dto.TotalCopies,
                    AvailableCopies = dto.TotalCopies
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} books from catalog", books.Count);
        }

        private sealed class BookSeedDto
        {
            public string Title { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public string Isbn { get; set; } = string.Empty;
            public string Genre { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
            public int TotalCopies { get; set; }
        }
    }
}
