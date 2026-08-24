using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Entities
{
    public class Book
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Author { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string ISBN { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Genre { get; set; } = string.Empty;

        public string? Language { get; set; }

        public string? Description { get; set; }

        [Range(0, 10000)]
        public int TotalCopies { get; set; }

        [Range(0, 10000)]
        public int AvailableCopies { get; set; }

        public ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();
    }
}
