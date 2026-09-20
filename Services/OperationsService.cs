using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Services;

public sealed class OperationsService(ApplicationDbContext db, StockService stock)
{
    public async Task ReceiveAsync(int itemId, int accepted, int rejected, int damaged, string batch,
        DateTime? expiry, string reason, string requestId)
    {
        ValidateRequest(requestId);
        var previous = await db.PurchaseReceipts.SingleOrDefaultAsync(r => r.RequestId == requestId);
        if (previous != null)
        {
            BusinessRule.Require(previous.PurchaseItemId == itemId && previous.AcceptedQuantity == accepted &&
                previous.RejectedQuantity == rejected && previous.DamagedQuantity == damaged, "Request ID already used for a different receipt.");
            return;
        }
        var item = await db.PurchaseItems.Include(i => i.Purchase).ThenInclude(p => p!.PurchaseItems).SingleAsync(i => i.Id == itemId);
        BusinessRule.Require(!item.Purchase!.IsArchived && item.Purchase.Status is "Pending" or "Partially Received", "This purchase cannot receive items.");
        BusinessRule.Require(accepted >= 0 && rejected >= 0 && damaged >= 0 && (long)accepted + rejected + damaged > 0, "Enter quantities received; all quantities must be non-negative.");
        var received = await db.PurchaseReceipts.Where(r => r.PurchaseItemId == itemId)
            .SumAsync(r => (long)r.AcceptedQuantity + r.RejectedQuantity + r.DamagedQuantity);
        BusinessRule.Require(received + accepted + rejected + damaged <= item.Quantity, "Receipt exceeds the outstanding ordered quantity.");
        BusinessRule.Require((rejected == 0 && damaged == 0) || !string.IsNullOrWhiteSpace(reason), "Explain rejected or damaged goods.");
        BusinessRule.Require(accepted == 0 || expiry == null || expiry.Value.Date > DateTime.Today, "Expired goods cannot be accepted into usable stock.");
        if (accepted > 0) await stock.ReceiveAsync(item.ProductId, accepted, batch, expiry, false, $"Purchase #{item.PurchaseId}: {reason}");
        db.PurchaseReceipts.Add(new PurchaseReceipt { PurchaseItemId = itemId, AcceptedQuantity = accepted,
            RejectedQuantity = rejected, DamagedQuantity = damaged, Reason = reason, RequestId = requestId, Actor = db.Actor });
        item.Purchase.Status = "Partially Received";
        await db.SaveChangesAsync();
        var ids = item.Purchase.PurchaseItems.Select(i => i.Id).ToList();
        var totals = await db.PurchaseReceipts.Where(r => ids.Contains(r.PurchaseItemId)).GroupBy(r => r.PurchaseItemId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(r => r.AcceptedQuantity + r.RejectedQuantity + r.DamagedQuantity) }).ToDictionaryAsync(r => r.Id, r => r.Quantity);
        if (item.Purchase.PurchaseItems.All(i => totals.GetValueOrDefault(i.Id) == i.Quantity))
            item.Purchase.Status = "Received";
        await db.SaveChangesAsync();
    }

    public async Task DeliverAsync(int deliveryId, int itemId, int quantity, string receivedBy, string proof, string requestId)
    {
        ValidateRequest(requestId);
        var previous = await db.DeliveryReceipts.SingleOrDefaultAsync(r => r.RequestId == requestId);
        if (previous != null)
        {
            BusinessRule.Require(previous.DeliveryId == deliveryId && previous.SalesOrderItemId == itemId &&
                previous.Quantity == quantity && previous.ReceivedBy == receivedBy && previous.ProofReference == proof,
                "Request ID already used for a different delivery.");
            return;
        }
        var delivery = await db.Deliveries.Include(d => d.SalesOrder).ThenInclude(o => o!.SalesOrderItems).SingleAsync(d => d.Id == deliveryId);
        BusinessRule.Require(!delivery.IsArchived && delivery.Status != "Delivered" && delivery.SalesOrder!.Status != "Cancelled", "This delivery cannot be updated.");
        var item = delivery.SalesOrder!.SalesOrderItems.SingleOrDefault(i => i.Id == itemId);
        BusinessRule.Require(item != null && quantity > 0, "Select an order item and positive quantity.");
        BusinessRule.Require(!string.IsNullOrWhiteSpace(receivedBy) && !string.IsNullOrWhiteSpace(proof), "Recipient and proof of delivery reference are required.");
        var delivered = await db.DeliveryReceipts.Where(r => r.SalesOrderItemId == itemId).SumAsync(r => r.Quantity);
        BusinessRule.Require((long)delivered + quantity <= item!.Quantity, "Delivered quantity exceeds the order.");
        db.DeliveryReceipts.Add(new DeliveryReceipt { DeliveryId = deliveryId, SalesOrderItemId = itemId,
            Quantity = quantity, ReceivedBy = receivedBy, ProofReference = proof, RequestId = requestId, Actor = db.Actor });
        delivery.Status = "Partially Delivered";
        delivery.SalesOrder.Status = "Partially Delivered";
        await db.SaveChangesAsync();
        var ids = delivery.SalesOrder.SalesOrderItems.Select(i => i.Id).ToList();
        var totals = await db.DeliveryReceipts.Where(r => ids.Contains(r.SalesOrderItemId)).GroupBy(r => r.SalesOrderItemId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(r => r.Quantity) }).ToDictionaryAsync(r => r.Id, r => r.Quantity);
        if (delivery.SalesOrder.SalesOrderItems.All(i => totals.GetValueOrDefault(i.Id) == i.Quantity))
        { delivery.Status = "Delivered"; delivery.SalesOrder.Status = "Delivered"; }
        await db.SaveChangesAsync();
    }

    public async Task ReturnAsync(int itemId, int quantity, string reason, string requestId)
    {
        ValidateRequest(requestId);
        var previous = await db.CustomerReturns.SingleOrDefaultAsync(r => r.RequestId == requestId);
        if (previous != null)
        {
            BusinessRule.Require(previous.SalesOrderItemId == itemId && previous.Quantity == quantity && previous.Reason == reason, "Request ID already used.");
            return;
        }
        var item = await db.SalesOrderItems.Include(i => i.SalesOrder).SingleAsync(i => i.Id == itemId);
        BusinessRule.Require(quantity > 0 && !string.IsNullOrWhiteSpace(reason), "Enter a positive quantity and return reason.");
        var delivered = await db.DeliveryReceipts.Where(r => r.SalesOrderItemId == itemId).SumAsync(r => r.Quantity);
        var returned = await db.CustomerReturns.Where(r => r.SalesOrderItemId == itemId).SumAsync(r => r.Quantity);
        BusinessRule.Require((long)returned + quantity <= delivered, "Return quantity exceeds delivered units not yet returned.");
        // Returned food must be inspected before anyone releases it for sale.
        await stock.ReceiveAsync(item.ProductId, quantity, $"RETURN-{Guid.NewGuid():N}", null, true, reason);
        db.CustomerReturns.Add(new CustomerReturn { SalesOrderItemId = itemId, Quantity = quantity,
            Reason = reason, Actor = db.Actor, RequestId = requestId });
        db.AuditReason = reason;
        await db.SaveChangesAsync();
    }

    private static void ValidateRequest(string requestId) => BusinessRule.Require(!string.IsNullOrWhiteSpace(requestId) && requestId.Length <= 100, "A request ID is required.");
}
