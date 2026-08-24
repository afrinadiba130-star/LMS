using WebApplication1.Models.Entities;

namespace WebApplication1.ViewModels
{
    public class ReportsIndexViewModel
    {
        public List<Invoice> UnpaidInvoices { get; set; } = new();
        public List<Invoice> RecentInvoices { get; set; } = new();
    }
}
