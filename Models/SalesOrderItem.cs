using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class SalesOrderItem
    {
        public int Id { get; set; }

        [Required]
        public int SalesOrderId { get; set; }

        public SalesOrder? SalesOrder { get; set; }

        [Required]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        public decimal Subtotal { get; set; }
    }
}