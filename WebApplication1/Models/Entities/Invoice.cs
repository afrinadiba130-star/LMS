using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Entities
{
    public class Invoice
    {
        public int Id { get; set; }

        public int BorrowRecordId { get; set; }
        public BorrowRecord BorrowRecord { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Range(0, 1000000)]
        public decimal TotalFine { get; set; }

        public DateTime IssuedDate { get; set; }

        public bool IsPaid { get; set; }
    }
}
