using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models;

public sealed class AdminRegistrationViewModel
{
    [Required, Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Username { get; set; } = "";

    [Required, RegularExpression("^(Admin|Manager)$", ErrorMessage = "Choose Admin or Manager.")]
    public string Role { get; set; } = "Manager";

    [Required, MinLength(12), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
