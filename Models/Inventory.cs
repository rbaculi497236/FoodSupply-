using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Inventory
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Display(Name = "Stock Quantity")]
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Display(Name = "Reorder Level")]
        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; }

        [Display(Name = "Stock Status")]
        public string StockStatus { get; set; } = "In Stock";

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [Display(Name = "Expiration Date")]
        public DateTime? ExpirationDate { get; set; }

        [Display(Name = "Spoiled Quantity")]
        [Range(0, int.MaxValue)]
        public int SpoiledQuantity { get; set; }

        [Display(Name = "Damaged Quantity")]
        [Range(0, int.MaxValue)]
        public int DamagedQuantity { get; set; }
    }
}