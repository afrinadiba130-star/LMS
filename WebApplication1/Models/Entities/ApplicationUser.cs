using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace WebApplication1.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        public int UserTypeId { get; set; }

        public UserType UserType { get; set; } = null!;

        public ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
