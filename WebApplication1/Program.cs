using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
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

            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            string connectionString;
            bool usePostgres;

            if (!string.IsNullOrEmpty(databaseUrl) && databaseUrl.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(databaseUrl);
                var connBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.Port > 0 ? uri.Port : 5432,
                    Database = uri.AbsolutePath.TrimStart('/'),
                    Username = uri.UserInfo.Split(':')[0],
                    Password = uri.UserInfo.Split(':').Length > 1 ? uri.UserInfo.Split(':')[1] : "",
                    SslMode = SslMode.Require,
                    TrustServerCertificate = true
                };
                connectionString = connBuilder.ConnectionString;
                usePostgres = true;
                Console.WriteLine($"Using PostgreSQL: Host={connBuilder.Host}, Port={connBuilder.Port}, Database={connBuilder.Database}");
            }
            else
            {
                var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
                Directory.CreateDirectory(dataDir);
                var dbPath = Path.Combine(dataDir, "library.db");
                connectionString = $"Data Source={dbPath}";
                usePostgres = false;
                Console.WriteLine($"Using SQLite: {dbPath}");
            }

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                if (usePostgres)
                    options.UseNpgsql(connectionString);
                else
                    options.UseSqlite(connectionString);
            });

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
