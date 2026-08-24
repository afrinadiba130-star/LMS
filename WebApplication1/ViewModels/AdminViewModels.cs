using System.ComponentModel.DataAnnotations;
using WebApplication1.Models.Entities;

namespace WebApplication1.ViewModels
{
    public class MemberListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int ActiveBorrows { get; set; }
        public decimal UnpaidFines { get; set; }
    }

    public class MemberDetailsViewModel
    {
        public ApplicationUser Member { get; set; } = null!;
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> UserTypes { get; set; } = new();
    }

    public class UserTypeEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Type Name")]
        public string TypeName { get; set; } = string.Empty;

        [Required, Range(1, 50), Display(Name = "Max Borrow Limit")]
        public int MaxBorrowLimit { get; set; }

        [Required, Range(0, 1000), Display(Name = "Fine Rate Per Day (Tk)")]
        public decimal FineRatePerDay { get; set; }

        [Required, Range(0, 100000), Display(Name = "Monthly Fee (Tk)")]
        public decimal MonthlyFee { get; set; }

        public string? Description { get; set; }
    }
}