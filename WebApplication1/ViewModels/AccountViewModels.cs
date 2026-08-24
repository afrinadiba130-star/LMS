using System.ComponentModel.DataAnnotations;

namespace WebApplication1.ViewModels
{
    public class RegisterViewModel
    {
        [Required, Display(Name = "Full Name"), MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Confirm Password")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required, Display(Name = "Member Type")]
        public int UserTypeId { get; set; }

        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> UserTypes { get; set; } = new();

        [Display(Name = "bKash TrxID")]
        [StringLength(50, MinimumLength = 5, ErrorMessage = "Enter a valid bKash TrxID (e.g. 9XK7H2M5A1).")]
        public string? BkashTrxId { get; set; }

        [Display(Name = "bKash Sender Number (01XXXXXXXXX)")]
        [StringLength(20, MinimumLength = 11, ErrorMessage = "Enter the 11-digit mobile number used for the payment.")]
        public string? BkashSenderNumber { get; set; }
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
