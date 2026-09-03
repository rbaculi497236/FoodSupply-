using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models
{
    public class Billing
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Sales Order")]
        public int SalesOrderId { get; set; }

        public SalesOrder? SalesOrder { get; set; }

        [Required]
        [Display(Name = "Invoice Date")]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);

        [Required]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = "Unpaid";

        [Range(0, double.MaxValue)]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Range(0, double.MaxValue)]
        [Display(Name = "Amount Paid")]
        public decimal AmountPaid { get; set; }

        [Display(Name = "Balance")]
        public decimal Balance { get; set; }

        [Display(Name = "Payment Date")]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        public bool IsArchived { get; set; } = false;
    }
}
