using WebApplication1.Models.Entities;
using WebApplication1.Services;

namespace WebApplication1.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalBooks { get; set; }
        public int TotalCopies { get; set; }
        public int ActiveBorrows { get; set; }
        public int OverdueBorrows { get; set; }
        public decimal UnpaidFines { get; set; }
        public List<MostBorrowedBookDto> MostBorrowedBooks { get; set; } = new();
        public List<RecommendedBookDto> Recommendations { get; set; } = new();
        public List<BorrowRecord> OverdueRecords { get; set; } = new();
        public int MyBorrowLimit { get; set; }
        public int MyActiveBorrows { get; set; }
        public List<MyPaymentDto> MyPayments { get; set; } = new();
    }

    public class MyPaymentDto
    {
        public int PaymentId { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string BkashTrxId { get; set; } = string.Empty;
        public DateTime PaidDate { get; set; }
        public int? InvoiceId { get; set; }
    }
}
