using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Services;

public sealed class SalesOrderService(ApplicationDbContext db, StockService stock)
{
    public async Task<SalesOrder> SaveAsync(SalesOrder input, int? id = null)
    {
        SalesOrder order;
        if (id.HasValue)
        {
            order = await db.SalesOrders.Include(o => o.SalesOrderItems).SingleAsync(o => o.Id == id);
            BusinessRule.Require(!order.IsArchived && order.Status is "Pending" or "Processing" or "Cancelled",
                "Only active pending, processing, or cancelled orders can be edited.");
            BusinessRule.Require(input.Status is "Pending" or "Processing" or "Cancelled", "Invalid order status.");
            if (order.Status != "Cancelled") await stock.ReleaseAsync(order);
            if (input.Status == "Cancelled")
            {
                // Keep original line prices and total for historical reporting.
                order.Status = "Cancelled";
                order.Remarks = input.Remarks;
                await db.SaveChangesAsync();
                return order;
            }
        }
        else { order = new SalesOrder(); db.SalesOrders.Add(order); }
        BusinessRule.Require(await db.Customers.AnyAsync(c => c.Id == input.CustomerId && !c.IsArchived), "Select an active customer.");
        BusinessRule.Require(input.SalesOrderItems.Count > 0, "Add at least one product.");
        var items = new List<SalesOrderItem>();
        foreach (var group in input.SalesOrderItems.GroupBy(i => i.ProductId))
        {
            BusinessRule.Require(group.All(i => i.Quantity > 0), "Quantities must be positive.");
            long quantity = group.Sum(i => (long)i.Quantity);
            BusinessRule.Require(quantity <= int.MaxValue, "Quantity is too large.");
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == group.Key && !p.IsArchived && p.Status == "Active");
            BusinessRule.Require(product != null, "A selected product is unavailable.");
            items.Add(new SalesOrderItem { ProductId = group.Key, Quantity = (int)quantity,
                UnitPrice = product!.Price, Subtotal = product.Price * quantity });
        }
        if (id.HasValue) db.SalesOrderItems.RemoveRange(order.SalesOrderItems);
        order.SalesOrderItems = items;
        order.CustomerId = input.CustomerId;
        order.Status = id.HasValue ? input.Status : "Pending";
        order.Remarks = input.Remarks;
        order.TotalAmount = items.Sum(i => i.Subtotal);
        await stock.AllocateAsync(order);
        await db.SaveChangesAsync();
        return order;
    }
}
