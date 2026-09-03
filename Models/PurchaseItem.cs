using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class PurchaseItem
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseId { get; set; }

        public Purchase? Purchase { get; set; }

        [Required]
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Display(Name = "Unit Price")]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Subtotal")]
        public decimal Subtotal { get; set; }
    }
}