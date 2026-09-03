using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
public class Customer
{
public int Id { get; set; }

    [Required]
    [Display(Name = "Customer Code")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }

    [Display(Name = "Customer Type")]
    public string CustomerType { get; set; } = "Regular";

    public string Status { get; set; } = "Active";
}

}
