
using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Purchase
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Purchase Order Number")]
        public string PurchaseOrderNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        public Supplier? Supplier { get; set; }

        [Required]
        [Display(Name = "Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        [Display(Name = "Total Amount")]
        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        // Archive status
        public bool IsArchived { get; set; } = false;

        public string? Notes { get; set; }

        public ICollection<PurchaseItem> PurchaseItems { get; set; }
            = new List<PurchaseItem>();
    }
}