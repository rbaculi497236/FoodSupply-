using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models;

public class CustomerActivity
{
    public int Id { get; set; }

    [Required]
    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    [Required]
    [Display(Name = "Activity Type")]
    public string ActivityType { get; set; } = "Note";

    [Required]
    public string Subject { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Display(Name = "Activity Date")]
    public DateTime ActivityDate { get; set; } = DateTime.Now;

    [Display(Name = "Next Action")]
    [DataType(DataType.Date)]
    public DateTime? NextActionDate { get; set; }
}