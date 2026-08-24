using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? builder.Configuration.GetConnectionString("LibraryDb");
            Console.WriteLine($"DATABASE_URL raw: {Environment.GetEnvironmentVariable("DATABASE_URL")}");
            Console.WriteLine($"Connection string before parse: '{databaseUrl}'");

            string connectionString;
            if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            {
                var connBuilder = new NpgsqlConnectionStringBuilder(databaseUrl);
                connectionString = connBuilder.ConnectionString;
                Console.WriteLine($"Parsed connection string: Host={connBuilder.Host}, Port={connBuilder.Port}, Database={connBuilder.Database}, Username={connBuilder.Username}");
            }
            else
            {
                connectionString = databaseUrl;
            }

            Console.WriteLine($"Final connection string: {connectionString}");
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
