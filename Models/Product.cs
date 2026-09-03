using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Product Code")]
        public string ProductCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;
    
        [Required]
        public string Unit { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Display(Name = "Expiration Date")]
        public DateTime? ExpirationDate { get; set; }

        public string Status { get; set; } = "Active";
    }
}
