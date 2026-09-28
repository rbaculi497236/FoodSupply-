using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models;

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(12), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match."),
     DataType(DataType.Password), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = "";
}
