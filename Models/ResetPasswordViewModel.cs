using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
public class ResetPasswordViewModel
{
[Required]
public int UserId { get; set; }
    [Required] public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(
        "NewPassword",
        ErrorMessage = "Passwords do not match."
    )]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

}
