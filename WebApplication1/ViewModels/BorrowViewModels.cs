using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Entities;

namespace WebApplication1.ViewModels
{
    public class BorrowIssueViewModel
    {
        [Required, Display(Name = "Member")]
        public string UserId { get; set; } = string.Empty;

        [Required, Display(Name = "Book")]
        public int BookId { get; set; }

        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Users { get; set; } = new();
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Books { get; set; } = new();
    }

    public class ReturnViewModel
    {
        [Required, Display(Name = "Borrow Record")]
        public int BorrowRecordId { get; set; }

        public string? BookTitle { get; set; }
        public string? MemberName { get; set; }
        public string? MemberType { get; set; }
        public DateTime? BorrowDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal FineRatePerDay { get; set; }
        public int OverdueDays { get; set; }
        public decimal ProjectedFine { get; set; }

        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> BorrowRecords { get; set; } = new();
    }
}
