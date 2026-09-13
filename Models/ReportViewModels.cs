using System;

namespace FoodSupply.Models
{
    // =========================================================
    // REPORTS DASHBOARD
    // =========================================================

    public class ReportsDashboardViewModel
    {
        public int TotalProducts { get; set; }

        public int TotalCustomers { get; set; }

        public int TotalSuppliers { get; set; }

        public int TotalOrders { get; set; }

        public int TotalDeliveries { get; set; }

        public int LowStockProducts { get; set; }

        public decimal TotalSales { get; set; }

        public decimal TotalPurchases { get; set; }

        public decimal TotalBilled { get; set; }

        public decimal TotalPaid { get; set; }

        public decimal TotalBalance { get; set; }
    }


    // =========================================================
    // SALES REPORT
    // =========================================================

    public class SalesReportViewModel
    {
        public int Id { get; set; }

        public DateTime OrderDate { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public int ItemCount { get; set; }
    }


    // =========================================================
    // INVENTORY REPORT
    // =========================================================

    public class InventoryReportViewModel
    {
        public string ProductCode { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public int StockQuantity { get; set; }

        public int ReorderLevel { get; set; }

        public int DamagedQuantity { get; set; }

        public int SpoiledQuantity { get; set; }

        public string StockStatus { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; }
    }


    // =========================================================
    // PURCHASE REPORT
    // =========================================================

    public class PurchaseReportViewModel
    {
        public int Id { get; set; }

        public string PurchaseOrderNumber { get; set; } = string.Empty;

        public DateTime PurchaseDate { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }
    }


    // =========================================================
    // BILLING REPORT
    // =========================================================

    public class BillingReportViewModel
    {
        public int Id { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }

        public DateTime DueDate { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public decimal AmountPaid { get; set; }

        public decimal Balance { get; set; }

        public DateTime? PaymentDate { get; set; }

        public string? PaymentMethod { get; set; }
    }


    // =========================================================
    // DELIVERY REPORT
    // =========================================================

    public class DeliveryReportViewModel
    {
        public int Id { get; set; }

        public DateTime DeliveryDate { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? DeliveryAddress { get; set; }

        public string? Driver { get; set; }

        public string? Vehicle { get; set; }
    }
}
