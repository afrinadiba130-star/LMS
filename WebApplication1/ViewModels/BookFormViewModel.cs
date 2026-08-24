using System.ComponentModel.DataAnnotations;

namespace WebApplication1.ViewModels
{
    public class BookFormViewModel
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

        [MaxLength(50)]
        public string? Language { get; set; }

        public string? Description { get; set; }

        [Required, Range(1, 10000)]
        public int TotalCopies { get; set; }
    }
}
