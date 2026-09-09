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

        // Foreign key to Category
        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        // Navigation property
        public Category? Category { get; set; }


        // Foreign key to Supplier
        [Required]
        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        // Navigation property
        public Supplier? Supplier { get; set; }


        [Required]
        public string Unit { get; set; } = string.Empty;

        [Display(Name = "Boxes")]
        [Range(0, int.MaxValue)]
        public int Boxes { get; set; }

        [Display(Name = "Pieces per Box")]
        [Range(1, int.MaxValue)]
        public int PiecesPerBox { get; set; } = 1;

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Display(Name = "Expiration Date")]
        public DateTime? ExpirationDate { get; set; }

        public string Status { get; set; } = "Active";

        // Archive instead of deleting
        public bool IsArchived { get; set; } = false;
    }
}