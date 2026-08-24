using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using WebApplication1.Data;
using WebApplication1.Models.Entities;
using WebApplication1.Services;

namespace WebApplication1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            QuestPDF.Settings.License = LicenseType.Community;

            builder.Services.AddControllersWithViews();

            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? builder.Configuration.GetConnectionString("LibraryDb");
            Console.WriteLine($"DATABASE_URL raw: {Environment.GetEnvironmentVariable("DATABASE_URL")}");
            Console.WriteLine($"Connection string before trim: '{connectionString}'");
            connectionString = connectionString?.Trim('"', '\'');
            Console.WriteLine($"Connection string after trim: '{connectionString}'");
            if (!string.IsNullOrEmpty(connectionString) && connectionString.StartsWith("postgres://"))
                connectionString = "postgresql://" + connectionString.Substring("postgres://".Length);
            Console.WriteLine($"Final connection string prefix: {connectionString?.Substring(0, Math.Min(30, connectionString.Length))}");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = 6;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

            builder.Services.AddScoped<IBorrowService, BorrowService>();
            builder.Services.AddScoped<IPdfReportService, PdfReportService>();
            builder.Services.AddScoped<IDbSeeder, DbSeeder>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Database.Migrate();

                var seeder = scope.ServiceProvider.GetRequiredService<IDbSeeder>();
                seeder.SeedAsync().GetAwaiter().GetResult();
            }

            app.Run();
        }
    }
}
