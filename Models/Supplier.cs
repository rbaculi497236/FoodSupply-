using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
public class Supplier
{
public int Id { get; set; }

    [Required]
    [Display(Name = "Supplier Code")]
    public string SupplierCode { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Supplier Name")]
    public string SupplierName { get; set; } = string.Empty;

    [Display(Name = "Contact Person")]
    public string? ContactPerson { get; set; }

    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }

    public string Status { get; set; } = "Active";
}

}
