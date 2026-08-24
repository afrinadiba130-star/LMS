using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Entities
{
    public class UserType
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string TypeName { get; set; } = string.Empty;

        [Range(1, 50)]
        public int MaxBorrowLimit { get; set; }

        [Range(0, 1000)]
        public decimal FineRatePerDay { get; set; }

        [Range(0, 100000)]
        public decimal MonthlyFee { get; set; }

        public string? Description { get; set; }
    }
}
