using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Entities
{
    public class BorrowRecord
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;

        public DateTime BorrowDate { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? ReturnDate { get; set; }

        public decimal FineAmount { get; set; }

        public bool IsReturned { get; set; }

        public Invoice? Invoice { get; set; }
    }
}
