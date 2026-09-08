namespace FoodSupply.Models
{
public class DashboardViewModel
{
// Summary counts
public int TotalProducts { get; set; }
public int TotalCustomers { get; set; }
public int TotalSuppliers { get; set; }
public int TotalCategories { get; set; }
public int TotalUsers { get; set; }

    // Inventory
    public int LowStockProducts { get; set; }

    // Purchasing
    public int PendingPurchases { get; set; }

    // Sales
    public int PendingSalesOrders { get; set; }
    public decimal TotalSales { get; set; }

    // Deliveries
    public int PendingDeliveries { get; set; }
    public int OutForDelivery { get; set; }
    public int CompletedDeliveries { get; set; }

    // Billing
    public int UnpaidInvoices { get; set; }
    public int PartiallyPaidInvoices { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal TotalPaid { get; set; }

    // Recent records
    public List<SalesOrder> RecentSalesOrders { get; set; }
        = new List<SalesOrder>();

    public List<Billing> RecentBillings { get; set; }
        = new List<Billing>();
}

}
