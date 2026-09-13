using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Advertisement
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        // Optional image for the advertisement
        public string? ImagePath { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // Scheduled, Active, Expired
        [StringLength(30)]
        public string Status { get; set; } = "Scheduled";

        // Archive instead of delete
        public bool IsArchived { get; set; } = false;
    }
}