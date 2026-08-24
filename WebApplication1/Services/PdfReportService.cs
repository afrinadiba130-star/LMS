using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApplication1.Data;

namespace WebApplication1.Services
{
    public interface IPdfReportService
    {
        Task<ServiceResult> GenerateFineInvoicePdfAsync(int invoiceId);
        Task<ServiceResult> GeneratePaymentReceiptPdfAsync(int paymentId);
        Task<ServiceResult> GeneratePaymentsReportPdfAsync(string? type, int year, int month);
        Task<ServiceResult> GenerateMonthlyReportPdfAsync(int year, int month);
        Task<ServiceResult> GenerateMostBorrowedBooksPdfAsync(int topCount);
    }

    public class PdfReportService : IPdfReportService
    {
        private const string CurrencySymbol = "Tk";

        private readonly ApplicationDbContext _context;
        private readonly ILogger<PdfReportService> _logger;

        public PdfReportService(ApplicationDbContext context, ILogger<PdfReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static string GetFontFamily()
        {
            if (File.Exists(@"C:\Windows\Fonts\Nirmala.ttc"))
                return "Nirmala UI";

            return "Arial";
        }

        public async Task<ServiceResult> GenerateFineInvoicePdfAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.BorrowRecord)
                    .ThenInclude(r => r.Book)
                .Include(i => i.BorrowRecord)
                    .ThenInclude(r => r.User)
                    .ThenInclude(u => u!.UserType)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice is null)
                return ServiceResult.Fail("Invoice not found.");

            var record = invoice.BorrowRecord;
            var days = (record.ReturnDate!.Value.Date - record.DueDate.Date).Days;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily(GetFontFamily()).FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Library Management System")
                                    .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                                c.Item().Text("Fine Invoice").FontSize(14).FontColor(Colors.Grey.Darken2);
                            });
                            row.ConstantItem(140).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Invoice #{invoice.Id:D6}").Bold().AlignRight();
                                c.Item().Text($"Issued: {invoice.IssuedDate:dd MMM yyyy, hh:mm tt}").FontSize(9).AlignRight();
                            });
                        });
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Billed To:").Bold();
                                c.Item().Text(record.User.FullName);
                                c.Item().Text(record.User.Email);
                                c.Item().Text($"Member Type: {record.User.UserType.TypeName}");
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("Book:").Bold().AlignRight();
                                c.Item().Text(record.Book.Title).AlignRight();
                                c.Item().Text(record.Book.Author).AlignRight();
                            });
                        });

                        col.Item().PaddingVertical(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Description").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Borrow Date").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Due Date").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Return Date").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Days Late").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Rate/Day").FontColor(Colors.White).Bold();
                            });

                            table.Cell().Padding(6).Text("Late return fine").Bold();
                            table.Cell().Padding(6).Text($"{record.BorrowDate:dd MMM yyyy}");
                            table.Cell().Padding(6).Text($"{record.DueDate:dd MMM yyyy}");
                            table.Cell().Padding(6).Text($"{record.ReturnDate:dd MMM yyyy}");
                            table.Cell().Padding(6).Text(days > 0 ? days.ToString() : "0");
                            table.Cell().Padding(6).Text($"{CurrencySymbol} {record.User.UserType.FineRatePerDay:N2}");
                        });

                        col.Item().PaddingTop(16);

                        col.Item().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Total Fine (BDT):  {CurrencySymbol} {invoice.TotalFine:N2}")
                                .FontSize(15).Bold().FontColor(Colors.Red.Darken3);
                            c.Item().PaddingTop(6).Text(invoice.IsPaid ? "Status: PAID" : "Status: UNPAID")
                                .Bold()
                                .FontColor(invoice.IsPaid ? Colors.Green.Darken2 : Colors.Red.Darken3);
                        });
                    });

                    page.Footer().AlignCenter().Text("Thank you for using our library. Please settle dues at the circulation desk.")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();

            return ServiceResult.Ok(data: pdf);
        }

        public async Task<ServiceResult> GeneratePaymentReceiptPdfAsync(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.User)
                    .ThenInclude(u => u!.UserType)
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment is null)
                return ServiceResult.Fail("Payment not found.");

            var isFine = payment.PaymentType == AppConstants.FinePayment;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily(GetFontFamily()).FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Library Management System")
                                    .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                                c.Item().Text("Payment Receipt").FontSize(14).FontColor(Colors.Grey.Darken2);
                            });
                            row.ConstantItem(160).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Receipt #P{payment.Id:D6}").Bold().AlignRight();
                                c.Item().Text($"Paid: {payment.PaidDate:dd MMM yyyy, hh:mm tt}").FontSize(9).AlignRight();
                            });
                        });
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Paid By:").Bold();
                                c.Item().Text(payment.User.FullName);
                                c.Item().Text(payment.User.Email);
                                c.Item().Text($"Member Type: {payment.User.UserType?.TypeName}");
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text(isFine ? "Invoice #:" : "Membership:").Bold().AlignRight();
                                c.Item().Text(isFine && payment.Invoice != null ? $"#{payment.Invoice.Id:D6}" : "Monthly membership fee").AlignRight();
                                c.Item().Text(isFine ? $"Due: {payment.Invoice?.BorrowRecord?.Book.Title}" : "Paid via bKash Send Money").AlignRight();
                            });
                        });

                        col.Item().PaddingVertical(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Description").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Type").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("bKash Number").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("TrxID").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Sender").FontColor(Colors.White).Bold();
                            });

                            table.Cell().Padding(6).Text(isFine ? "Fine payment" : "Membership fee").Bold();
                            table.Cell().Padding(6).Text(isFine ? "Fine" : "Membership");
                            table.Cell().Padding(6).Text(payment.BkashNumber);
                            table.Cell().Padding(6).Text(payment.BkashTrxId);
                            table.Cell().Padding(6).Text(payment.SenderNumber);
                        });

                        col.Item().PaddingTop(16);

                        col.Item().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Amount Paid (BDT):  {CurrencySymbol} {payment.Amount:N2}")
                                .FontSize(15).Bold().FontColor(Colors.Green.Darken2);
                            c.Item().PaddingTop(6).Text("Status: PAID").Bold().FontColor(Colors.Green.Darken2);
                        });
                    });

                    page.Footer().AlignCenter()
                        .Text($"bKash Send Money to {AppConstants.BkashNumber} — Thank you for your payment.")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();

            return ServiceResult.Ok(data: pdf);
        }

        public async Task<ServiceResult> GeneratePaymentsReportPdfAsync(string? type, int year, int month)
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);

            var query = _context.Payments
                .Include(p => p.User)
                    .ThenInclude(u => u!.UserType)
                .Include(p => p.Invoice)
                .Where(p => p.PaidDate >= from && p.PaidDate < to);

            if (!string.IsNullOrWhiteSpace(type) &&
                (type == AppConstants.MembershipPayment || type == AppConstants.FinePayment))
            {
                query = query.Where(p => p.PaymentType == type);
            }

            var payments = await query.OrderBy(p => p.PaidDate).ToListAsync();

            var membershipTotal = payments.Where(p => p.PaymentType == AppConstants.MembershipPayment).Sum(p => p.Amount);
            var fineTotal = payments.Where(p => p.PaymentType == AppConstants.FinePayment).Sum(p => p.Amount);

            var monthName = from.ToString("MMMM yyyy");

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily(GetFontFamily()).FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Library Management System")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Text($"bKash Payments Report — {monthName}")
                            .FontSize(14).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Membership Payments:  {CurrencySymbol} {membershipTotal:N2}").Bold();
                                c.Item().Text($"Fine Payments:        {CurrencySymbol} {fineTotal:N2}").Bold();
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Total Collected:  {CurrencySymbol} {membershipTotal + fineTotal:N2}")
                                    .FontSize(13).Bold().FontColor(Colors.Green.Darken2);
                            });
                        });

                        col.Item().PaddingTop(12);

                        if (payments.Count == 0)
                        {
                            col.Item().Text("No payments recorded for this period.").FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1.2f);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(1.2f);
                                    c.RelativeColumn(1.3f);
                                    c.RelativeColumn(1.2f);
                                    c.RelativeColumn(1);
                                });

                                table.Header(h =>
                                {
                                    var cells = new[]
                                    {
                                        "Date", "Type", "Member", "Amount (Tk)", "TrxID", "Sender", "Receipt #"
                                    };
                                    foreach (var text in cells)
                                    {
                                        h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text(text).FontColor(Colors.White).Bold();
                                    }
                                });

                                foreach (var p in payments)
                                {
                                    table.Cell().Padding(6).Text($"{p.PaidDate:dd MMM yyyy}");
                                    table.Cell().Padding(6).Text(p.PaymentType == AppConstants.MembershipPayment ? "Membership" : "Fine");
                                    table.Cell().Padding(6).Text(p.User.FullName);
                                    table.Cell().Padding(6).Text(p.Amount.ToString("N2"));
                                    table.Cell().Padding(6).Text(p.BkashTrxId);
                                    table.Cell().Padding(6).Text(p.SenderNumber);
                                    table.Cell().Padding(6).Text($"P{p.Id:D6}");
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text($"Generated on {DateTime.Now:dd MMM yyyy, hh:mm tt} by Library Management System")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();

            return ServiceResult.Ok(data: pdf);
        }

        public async Task<ServiceResult> GenerateMonthlyReportPdfAsync(int year, int month)
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);

            var totalBorrowed = await _context.BorrowRecords
                .CountAsync(r => r.BorrowDate >= from && r.BorrowDate < to);

            var totalReturned = await _context.BorrowRecords
                .CountAsync(r => r.ReturnDate >= from && r.ReturnDate < to);

            var finesCollected = await _context.Invoices
                .Where(i => i.IssuedDate >= from && i.IssuedDate < to)
                .SumAsync(i => (decimal?)i.TotalFine) ?? 0;

            var topBooks = await _context.BorrowRecords
                .Where(r => r.BorrowDate >= from && r.BorrowDate < to)
                .GroupBy(r => new { r.Book.Title, r.Book.Author, r.Book.Genre })
                .Select(g => new { g.Key.Title, g.Key.Author, g.Key.Genre, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            var monthName = from.ToString("MMMM yyyy");

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily(GetFontFamily()).FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Library Management System")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Text($"Monthly Borrowing Summary — {monthName}")
                            .FontSize(14).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Blue.Darken3).Padding(8).Text("Metric").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(8).Text("Value").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(8).Text("Note").FontColor(Colors.White).Bold();
                            });

                            table.Cell().Padding(8).Text("Books Borrowed").Bold();
                            table.Cell().Padding(8).Text(totalBorrowed.ToString());
                            table.Cell().Padding(8).Text("New loans issued this month");

                            table.Cell().Padding(8).Text("Books Returned").Bold();
                            table.Cell().Padding(8).Text(totalReturned.ToString());
                            table.Cell().Padding(8).Text("Returns processed this month");

                            table.Cell().Padding(8).Text("Fines Issued (BDT)").Bold();
                            table.Cell().Padding(8).Text($"{CurrencySymbol} {finesCollected:N2}");
                            table.Cell().Padding(8).Text("Late-return fines invoiced");
                        });

                        col.Item().PaddingTop(16).Text("Most Borrowed Books").FontSize(14).Bold();

                        if (topBooks.Count > 0)
                        {
                            col.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(0.5f);
                                    c.RelativeColumn(3);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(1.5f);
                                    c.RelativeColumn(1);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("#").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("Title").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("Author").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("Genre").Bold();
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(6).Text("Loans").Bold();
                                });

                                var rank = 1;
                                foreach (var book in topBooks)
                                {
                                    table.Cell().Padding(6).Text(rank.ToString());
                                    table.Cell().Padding(6).Text(book.Title);
                                    table.Cell().Padding(6).Text(book.Author);
                                    table.Cell().Padding(6).Text(book.Genre);
                                    table.Cell().Padding(6).Text(book.Count.ToString());
                                    rank++;
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(6).Text("No borrowing activity recorded for this month.")
                                .FontColor(Colors.Grey.Darken2);
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text($"Generated on {DateTime.Now:dd MMM yyyy, hh:mm tt} by Library Management System")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();

            return ServiceResult.Ok(data: pdf);
        }

        public async Task<ServiceResult> GenerateMostBorrowedBooksPdfAsync(int topCount)
        {
            var topBooks = await _context.BorrowRecords
                .Where(r => r.IsReturned)
                .GroupBy(r => new { r.Book.Title, r.Book.Author, r.Book.Genre, r.Book.AvailableCopies })
                .Select(g => new
                {
                    g.Key.Title,
                    g.Key.Author,
                    g.Key.Genre,
                    g.Key.AvailableCopies,
                    BorrowCount = g.Count()
                })
                .OrderByDescending(x => x.BorrowCount)
                .Take(topCount)
                .ToListAsync();

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(ts => ts.FontFamily(GetFontFamily()).FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Library Management System")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Text($"Top {topCount} Most Borrowed Books")
                            .FontSize(14).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(0.5f);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("#").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Title").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Author").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Genre").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Times Borrowed").FontColor(Colors.White).Bold();
                                h.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Available").FontColor(Colors.White).Bold();
                            });

                            var rank = 1;
                            foreach (var book in topBooks)
                            {
                                table.Cell().Padding(6).Text(rank.ToString());
                                table.Cell().Padding(6).Text(book.Title);
                                table.Cell().Padding(6).Text(book.Author);
                                table.Cell().Padding(6).Text(book.Genre);
                                table.Cell().Padding(6).Text(book.BorrowCount.ToString());
                                table.Cell().Padding(6).Text(book.AvailableCopies.ToString());
                                rank++;
                            }
                        });
                    });

                    page.Footer().AlignCenter()
                        .Text($"Generated on {DateTime.Now:dd MMM yyyy, hh:mm tt} by Library Management System")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();

            return ServiceResult.Ok(data: pdf);
        }
    }
}
