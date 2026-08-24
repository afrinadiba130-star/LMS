using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Entities
{
    public class Payment
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int? InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        [Required, MaxLength(20)]
        public string PaymentType { get; set; } = string.Empty;

        [Range(0, 1000000)]
        public decimal Amount { get; set; }

        [Required, MaxLength(20)]
        public string BkashNumber { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string BkashTrxId { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string SenderNumber { get; set; } = string.Empty;

        public DateTime PaidDate { get; set; }

        public bool IsVerified { get; set; }
    }
}