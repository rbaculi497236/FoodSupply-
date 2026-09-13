using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Promotion
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string PromotionCode { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Percentage or Fixed Amount
        [Required]
        [StringLength(30)]
        public string DiscountType { get; set; } = "Percentage";

        [Range(0, double.MaxValue)]
        public decimal DiscountValue { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // Scheduled, Active, Expired
        [StringLength(30)]
        public string Status { get; set; } = "Scheduled";

        // Archive instead of delete
        public bool IsArchived { get; set; } = false;
    }
}