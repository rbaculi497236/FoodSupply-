using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodSupply.Models
{
    public class CustomerConcern
    {
        public int Id { get; set; }

        // Customer who reported the concern
        [Required]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        // Complaint, Inquiry, Request, Feedback, etc.
        [Required]
        [StringLength(50)]
        public string ConcernType { get; set; } = "Complaint";

        [Required]
        [StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        // Low, Medium, High, Urgent
        [StringLength(20)]
        public string Priority { get; set; } = "Medium";

        // Pending, In Progress, Resolved
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        // Action taken to solve the concern
        public string? Resolution { get; set; }

        public DateTime DateReported { get; set; } = DateTime.Now;

        public DateTime? ResolvedDate { get; set; }

        public string? Remarks { get; set; }

        // Archive instead of delete
        public bool IsArchived { get; set; } = false;
    }
}