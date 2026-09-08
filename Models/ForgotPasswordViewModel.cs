using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
public class ForgotPasswordViewModel
{
[Required(ErrorMessage = "Email address is required.")]
[EmailAddress(ErrorMessage = "Please enter a valid email address.")]
[Display(Name = "Email Address")]
public string Email { get; set; } = string.Empty;
}
}
