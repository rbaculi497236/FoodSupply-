using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class SalesOrder
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        public Customer? Customer { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        public string Status { get; set; } = "Pending";

        public decimal TotalAmount { get; set; }

        public string? Remarks { get; set; }

        // Soft archive
        public bool IsArchived { get; set; } = false;

        public ICollection<SalesOrderItem> SalesOrderItems { get; set; }
            = new List<SalesOrderItem>();
    }
}