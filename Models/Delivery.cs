using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
public class Delivery
{
public int Id { get; set; }

    [Required]
    [Display(Name = "Sales Order")]
    public int SalesOrderId { get; set; }

    public SalesOrder? SalesOrder { get; set; }

    [Required]
    [Display(Name = "Delivery Date")]
    public DateTime DeliveryDate { get; set; } = DateTime.Now;

    [Required]
    [Display(Name = "Delivery Status")]
    public string Status { get; set; } = "Pending";

    [Display(Name = "Delivery Address")]
    public string? DeliveryAddress { get; set; }

    [Display(Name = "Driver")]
    public string? Driver { get; set; }

    [Display(Name = "Vehicle")]
    public string? Vehicle { get; set; }

    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    // Soft archive
    public bool IsArchived { get; set; } = false;
}

}
