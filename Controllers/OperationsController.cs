using FoodSupply.Data;
using FoodSupply.Models;
using FoodSupply.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers;

[Authorize]
public class OperationsController(ApplicationDbContext db, StockService stock, OperationsService operations) : Controller
{
    private const string Warehouse = "Admin,Manager,Main Admin,Warehouse Staff";
    private const string Purchasing = "Admin,Manager,Main Admin,Purchasing/Supplier Staff";
    private const string DeliveryRoles = "Admin,Manager,Main Admin,Delivery Staff";
    private const string Sales = "Admin,Manager,Main Admin,Warehouse Staff,Sales Staff / Billing Staff,Sales/Customer Staff";

    [Authorize(Roles = Warehouse)]
    public async Task<IActionResult> Batches(int productId)
    {
        var product = await db.Products.FindAsync(productId);
        if (product == null) return NotFound();
        ViewBag.Product = product;
        ViewBag.Movements = await db.StockMovements.Include(m => m.InventoryBatch)
            .Where(m => m.InventoryBatch!.ProductId == productId).OrderByDescending(m => m.Id).Take(100).ToListAsync();
        return View(await db.InventoryBatches.Where(b => b.ProductId == productId).OrderBy(b => b.ExpirationDate).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Warehouse)]
    public async Task<IActionResult> ReceiveStock(int productId, int quantity, string batchNumber, DateTime? expiry, string reason)
    {
        BusinessRule.Require(!string.IsNullOrWhiteSpace(reason), "A receipt reason is required.");
        BusinessRule.Require(expiry == null || expiry.Value.Date > DateTime.Today, "Expired goods must be recorded through a quarantined adjustment.");
        await stock.ReceiveAsync(productId, quantity, batchNumber, expiry, false, reason);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Batches), new { productId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Warehouse)]
    public async Task<IActionResult> Adjust(int batchId, int quantity, bool quarantined, string reason, string version)
    {
        var batch = await db.InventoryBatches.SingleAsync(b => b.Id == batchId);
        BusinessRule.Require(batch.Version == version, "This batch changed. Refresh before adjusting it.");
        await stock.AdjustAsync(batchId, quantity, quarantined, reason);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Batches), new { productId = batch.ProductId });
    }

    [Authorize(Roles = Purchasing)]
    public async Task<IActionResult> Receive(int id)
    {
        var purchase = await db.Purchases.Include(p => p.PurchaseItems).ThenInclude(i => i.Product).SingleOrDefaultAsync(p => p.Id == id);
        if (purchase == null) return NotFound();
        ViewBag.Receipts = await db.PurchaseReceipts.Where(r => r.PurchaseItem!.PurchaseId == id).OrderByDescending(r => r.Id).ToListAsync();
        return View(purchase);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Purchasing)]
    public async Task<IActionResult> ReceiveItem(int itemId, int accepted, int rejected, int damaged, string? batchNumber, DateTime? expiry, string? reason, string requestId)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await operations.ReceiveAsync(itemId, accepted, rejected, damaged, batchNumber ?? "", expiry, reason ?? "", requestId);
        var item = await db.PurchaseItems.FindAsync(itemId);
        return RedirectToAction(nameof(Receive), new { id = item!.PurchaseId });
    }

    [Authorize(Roles = DeliveryRoles)]
    public async Task<IActionResult> Fulfill(int id)
    {
        var delivery = await db.Deliveries.Include(d => d.SalesOrder).ThenInclude(o => o!.SalesOrderItems).ThenInclude(i => i.Product).SingleOrDefaultAsync(d => d.Id == id);
        if (delivery == null) return NotFound();
        ViewBag.Receipts = await db.DeliveryReceipts.Where(r => r.SalesOrderItem!.SalesOrderId == delivery.SalesOrderId).OrderByDescending(r => r.Id).ToListAsync();
        return View(delivery);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = DeliveryRoles)]
    public async Task<IActionResult> Deliver(int deliveryId, int itemId, int quantity, string receivedBy, string proof, string requestId)
    {
        await operations.DeliverAsync(deliveryId, itemId, quantity, receivedBy, proof, requestId);
        return RedirectToAction(nameof(Fulfill), new { id = deliveryId });
    }

    [Authorize(Roles = Sales)]
    public async Task<IActionResult> Returns(int id)
    {
        var order = await db.SalesOrders.Include(o => o.SalesOrderItems).ThenInclude(i => i.Product).SingleOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound();
        ViewBag.Returns = await db.CustomerReturns.Where(r => r.SalesOrderItem!.SalesOrderId == id).OrderByDescending(r => r.Id).ToListAsync();
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Sales)]
    public async Task<IActionResult> ReturnItem(int itemId, int quantity, string reason, string requestId)
    {
        await operations.ReturnAsync(itemId, quantity, reason, requestId);
        var item = await db.SalesOrderItems.FindAsync(itemId);
        return RedirectToAction(nameof(Returns), new { id = item!.SalesOrderId });
    }

    [Authorize(Roles = "Admin,Main Admin,Manager")]
    public async Task<IActionResult> Audit(int page = 1)
    {
        page = Math.Max(1, page);
        ViewBag.Page = page;
        var entries = await db.AuditEntries.AsNoTracking().OrderByDescending(a => a.Id).Skip((page - 1) * 50).Take(50).ToListAsync();
        var ids = entries.Select(a => int.TryParse(a.Actor, out var id) ? id : 0).Distinct().ToList();
        ViewBag.AuditUsers = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Role }).ToDictionaryAsync(u => u.Id.ToString(), u => new[] { u.FullName, u.Role });
        return View(entries);
    }
}
