namespace FoodSupply.Models;

public class DashboardHighlights
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool CanViewSales { get; set; }
    public bool CanViewInventory { get; set; }
    public bool CanViewActivity { get; set; }
    public bool CanViewDeliveries { get; set; }
    public int OrdersToday { get; set; }
    public int DeliveriesToday { get; set; }
    public int LowStock { get; set; }
    public ReportChartViewModel Sales { get; set; } = new() { Title = "Sales trend", Description = "Delivered orders by order month within the selected dates.", Currency = true, Type = "Line" };
    public ReportChartViewModel TopProducts { get; set; } = new() { Title = "Top-selling products", Description = "Top five products by units on delivered orders within the selected dates." };
    public ReportChartViewModel Stock { get; set; } = new() { Title = "Stock health", Description = "Current active inventory; independent of the date filter.", Type = "Doughnut" };
    public List<AuditEntry> Activity { get; set; } = [];
}
