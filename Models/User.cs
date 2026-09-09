using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        // Password is entered separately in the Create form.
        // The controller will assign the value to PasswordHash.
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Manager";

        public bool IsActive { get; set; } = true;

        public bool IsArchived { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}