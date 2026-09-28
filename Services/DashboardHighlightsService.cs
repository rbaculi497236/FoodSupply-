using System.Security.Claims;
using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Services;

public static class DashboardHighlightsService
{
    public static async Task<DashboardHighlights> LoadAsync(ApplicationDbContext db, ClaimsPrincipal user, DateTime from, DateTime to)
    {
        bool In(params string[] roles) => roles.Any(user.IsInRole);
        var management = In("Admin", "Main Admin", "Manager");
        var model = new DashboardHighlights { From = from, To = to,
            CanViewSales = management || In("Sales Staff / Billing Staff", "Sales/Customer Staff"),
            CanViewInventory = management || In("Warehouse Staff"), CanViewActivity = management,
            CanViewDeliveries = management || In("Delivery Staff") };
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var end = to.AddDays(1);
        if (model.CanViewSales)
        {
            model.OrdersToday = await db.SalesOrders.CountAsync(s => !s.IsArchived && s.OrderDate >= today && s.OrderDate < tomorrow);
            var orders = db.SalesOrders.AsNoTracking().Where(s => !s.IsArchived && s.Status == "Delivered" && s.OrderDate >= from && s.OrderDate < end);
            var totals = await orders.Select(s => new { s.OrderDate, s.TotalAmount }).ToListAsync();
            model.Sales.Points = totals.GroupBy(s => s.OrderDate.ToString("yyyy-MM")).OrderBy(g => g.Key)
                .Select(g => new ReportChartPoint { Label = g.Key, Value = g.Sum(s => s.TotalAmount) }).ToList();
            var items = await orders.SelectMany(s => s.SalesOrderItems).Select(i => new { i.ProductId, Name = i.Product!.ProductName, i.Quantity }).ToListAsync();
            model.TopProducts.Points = items.GroupBy(i => new { i.ProductId, i.Name })
                .Select(g => new ReportChartPoint { Label = g.Key.Name, Value = g.Sum(i => (decimal)i.Quantity) })
                .OrderByDescending(p => p.Value).ThenBy(p => p.Label).Take(5).ToList();
        }
        if (model.CanViewDeliveries)
            model.DeliveriesToday = await db.Deliveries.CountAsync(d => !d.IsArchived && d.Status != "Cancelled" && d.DeliveryDate >= today && d.DeliveryDate < tomorrow);
        if (model.CanViewInventory)
        {
            var inventory = await db.Inventories.AsNoTracking().Where(i => !i.IsArchived && !i.Product!.IsArchived)
                .Select(i => new { i.StockQuantity, i.ReorderLevel }).ToListAsync();
            model.LowStock = inventory.Count(i => i.StockQuantity <= i.ReorderLevel);
            model.Stock.Points = [
                new() { Label = "Healthy", Value = inventory.Count(i => i.StockQuantity > i.ReorderLevel) },
                new() { Label = "Low stock", Value = inventory.Count(i => i.StockQuantity > 0 && i.StockQuantity <= i.ReorderLevel) },
                new() { Label = "Out of stock", Value = inventory.Count(i => i.StockQuantity == 0) }
            ];
        }
        if (model.CanViewActivity)
            model.Activity = await db.AuditEntries.AsNoTracking()
                .Where(a => a.Entity == "SalesOrder" || a.Entity == "Payment" || a.Entity == "Delivery")
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Take(5)
                .Select(a => new AuditEntry { Id = a.Id, Entity = a.Entity, Action = a.Action, RecordId = a.RecordId, CreatedAt = a.CreatedAt }).ToListAsync();
        return model;
    }
}
