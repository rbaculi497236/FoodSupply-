using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models;

public class ProfileViewModel
{
    [Required, StringLength(200), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required, StringLength(100)]
    public string Username { get; set; } = "";

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";
}
