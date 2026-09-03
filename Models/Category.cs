using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category Code")]
        public string CategoryCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Status { get; set; } = "Active";
    }
}