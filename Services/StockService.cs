using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Services;

public sealed class StockService(ApplicationDbContext db)
{
    public async Task ReceiveAsync(int productId, int quantity, string batchNumber, DateTime? expiry,
        bool quarantined, string reason, SalesOrder? order = null)
    {
        BusinessRule.Require(quantity > 0, "Quantity must be positive.");
        BusinessRule.Require(!string.IsNullOrWhiteSpace(batchNumber) && batchNumber.Length <= 100, "Enter a batch number of up to 100 characters.");
        var product = await db.Products.SingleAsync(p => p.Id == productId);
        BusinessRule.Require(!product.IsArchived, "Archived products cannot receive stock.");
        var inventory = await GetInventoryAsync(productId);
        BusinessRule.Require(!inventory.IsArchived, "Restore this inventory before receiving stock.");
        var batch = new InventoryBatch { ProductId = productId, BatchNumber = batchNumber.Trim(),
            Quantity = quantity, ExpirationDate = expiry?.Date, IsQuarantined = quarantined };
        db.InventoryBatches.Add(batch);
        db.StockMovements.Add(new StockMovement { InventoryBatch = batch, Quantity = quantity,
            Reason = reason, Actor = db.Actor, SalesOrder = order });
        inventory.StockQuantity = checked(inventory.StockQuantity + quantity);
        Refresh(inventory);
    }

    public async Task AllocateAsync(SalesOrder order)
    {
        foreach (var group in order.SalesOrderItems.GroupBy(i => i.ProductId))
        {
            BusinessRule.Require(group.All(i => i.Quantity > 0), "Order quantities must be positive.");
            var requested = group.Sum(i => (long)i.Quantity);
            BusinessRule.Require(requested <= int.MaxValue, "Requested quantity is too large.");
            var inventory = await GetInventoryAsync(group.Key);
            BusinessRule.Require(!inventory.IsArchived, "Archived inventory cannot be sold.");
            var today = DateTime.Today;
            var batches = await db.InventoryBatches.Where(b => b.ProductId == group.Key)
                .OrderBy(b => b.ExpirationDate == null).ThenBy(b => b.ExpirationDate)
                .ThenBy(b => b.ReceivedAt).ThenBy(b => b.Id).ToListAsync();
            var usable = batches.Where(b => !b.IsQuarantined && b.Quantity > 0 &&
                (!b.ExpirationDate.HasValue || b.ExpirationDate.Value.Date > today)).ToList();
            BusinessRule.Require(usable.Sum(b => (long)b.Quantity) >= requested,
                $"Insufficient usable stock for product #{group.Key}. Expired and quarantined batches cannot be sold.");
            int remaining = (int)requested;
            foreach (var batch in usable)
            {
                int take = Math.Min(batch.Quantity, remaining);
                if (take == 0) break;
                batch.Quantity -= take;
                db.StockMovements.Add(new StockMovement { InventoryBatch = batch, SalesOrder = order,
                    Quantity = -take, Reason = "Sales allocation", Actor = db.Actor });
                remaining -= take;
            }
            inventory.StockQuantity -= (int)requested;
            Refresh(inventory);
        }
    }

    public async Task ReleaseAsync(SalesOrder order)
    {
        var movements = await db.StockMovements.Include(m => m.InventoryBatch)
            .Where(m => m.SalesOrderId == order.Id &&
                (m.Reason == "Sales allocation" || m.Reason == "Sales release")).ToListAsync();
        foreach (var group in movements.GroupBy(m => m.InventoryBatchId))
        {
            int quantity = -group.Sum(m => m.Quantity);
            if (quantity <= 0) continue;
            var batch = group.First().InventoryBatch!;
            batch.Quantity = checked(batch.Quantity + quantity);
            var inventory = await GetInventoryAsync(batch.ProductId);
            inventory.StockQuantity = checked(inventory.StockQuantity + quantity);
            Refresh(inventory);
            db.StockMovements.Add(new StockMovement { InventoryBatch = batch, SalesOrder = order,
                Quantity = quantity, Reason = "Sales release", Actor = db.Actor });
        }
    }

    public async Task AdjustAsync(int batchId, int quantity, bool quarantined, string reason)
    {
        BusinessRule.Require(quantity >= 0 && !string.IsNullOrWhiteSpace(reason), "A non-negative quantity and adjustment reason are required.");
        var batch = await db.InventoryBatches.SingleAsync(b => b.Id == batchId);
        var inventory = await GetInventoryAsync(batch.ProductId);
        BusinessRule.Require(!inventory.IsArchived, "Restore this inventory before adjusting it.");
        var delta = quantity - batch.Quantity;
        inventory.StockQuantity = checked(inventory.StockQuantity + delta);
        batch.Quantity = quantity;
        batch.IsQuarantined = quarantined;
        db.AuditReason = reason;
        db.StockMovements.Add(new StockMovement { InventoryBatch = batch, Quantity = delta,
            Reason = reason, Actor = db.Actor });
        Refresh(inventory);
    }

    private async Task<Inventory> GetInventoryAsync(int productId)
    {
        var inventory = db.Inventories.Local.FirstOrDefault(i => i.ProductId == productId)
            ?? await db.Inventories.SingleOrDefaultAsync(i => i.ProductId == productId);
        if (inventory != null) return inventory;
        var product = await db.Products.FindAsync(productId);
        inventory = new Inventory { ProductId = productId, ReorderLevel = product?.ReorderLevel ?? 0 };
        db.Inventories.Add(inventory);
        return inventory;
    }

    private static void Refresh(Inventory inventory)
    {
        inventory.LastUpdated = DateTime.UtcNow;
        inventory.StockStatus = inventory.StockQuantity == 0 ? "Out of Stock" :
            inventory.StockQuantity <= inventory.ReorderLevel ? "Low Stock" : "In Stock";
    }
}
