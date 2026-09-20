using System.ComponentModel.DataAnnotations;

namespace FoodSupply.Models;

public class InventoryBatch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, StringLength(100)] public string BatchNumber { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }
    public int Quantity { get; set; }
    public bool IsQuarantined { get; set; }
    public string Version { get; set; } = Guid.NewGuid().ToString();
}

public class StockMovement
{
    public int Id { get; set; }
    public int InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public int? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "system";
    public string Reason { get; set; } = "";
}

public class Payment
{
    public int Id { get; set; }
    public int BillingId { get; set; }
    public Billing? Billing { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    [StringLength(100)] public string Method { get; set; } = "";
    [StringLength(200)] public string Reference { get; set; } = "";
    [StringLength(100)] public string RequestId { get; set; } = "";
    public int? ReversesPaymentId { get; set; }
    public string Actor { get; set; } = "system";
    public string Reason { get; set; } = "";
}

public class PurchaseReceipt
{
    public int Id { get; set; }
    public int PurchaseItemId { get; set; }
    public PurchaseItem? PurchaseItem { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public int DamagedQuantity { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "system";
    public string Reason { get; set; } = "";
    [StringLength(100)] public string RequestId { get; set; } = "";
}

public class DeliveryReceipt
{
    public int Id { get; set; }
    public int DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }
    public int SalesOrderItemId { get; set; }
    public SalesOrderItem? SalesOrderItem { get; set; }
    public int Quantity { get; set; }
    public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;
    public string ReceivedBy { get; set; } = "";
    public string ProofReference { get; set; } = "";
    public string Actor { get; set; } = "system";
    [StringLength(100)] public string RequestId { get; set; } = "";
}

public class CustomerReturn
{
    public int Id { get; set; }
    public int SalesOrderItemId { get; set; }
    public SalesOrderItem? SalesOrderItem { get; set; }
    public int Quantity { get; set; }
    public DateTime ReturnedAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "system";
    [StringLength(100)] public string RequestId { get; set; } = "";
}

public class AuditEntry
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "system";
    public string Entity { get; set; } = "";
    public string RecordId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Changes { get; set; } = "";
    public string Reason { get; set; } = "";
}
