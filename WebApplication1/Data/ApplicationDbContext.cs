using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Entities;

namespace WebApplication1.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserType> UserTypes => Set<UserType>();
        public DbSet<Book> Books => Set<Book>();
        public DbSet<BorrowRecord> BorrowRecords => Set<BorrowRecord>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserType>()
                .HasIndex(u => u.TypeName)
                .IsUnique();

            builder.Entity<Book>()
                .HasIndex(b => b.ISBN)
                .IsUnique();

            builder.Entity<BorrowRecord>()
                .Property(r => r.FineAmount)
                .HasPrecision(12, 2);

            builder.Entity<Invoice>()
                .Property(i => i.TotalFine)
                .HasPrecision(12, 2);

            builder.Entity<UserType>()
                .Property(u => u.FineRatePerDay)
                .HasPrecision(12, 2);

            builder.Entity<UserType>()
                .Property(u => u.MonthlyFee)
                .HasPrecision(12, 2);

            builder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(12, 2);

            builder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany()
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<BorrowRecord>()
                .HasOne(b => b.User)
                .WithMany(u => u.BorrowRecords)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BorrowRecord>()
                .HasOne(b => b.Book)
                .WithMany(bk => bk.BorrowRecords)
                .HasForeignKey(b => b.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Invoice>()
                .HasOne(i => i.BorrowRecord)
                .WithOne(b => b.Invoice)
                .HasForeignKey<Invoice>(i => i.BorrowRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Invoice>()
                .HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
