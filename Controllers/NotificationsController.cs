using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers;

[Authorize(Roles = "Admin,Main Admin,Manager,Sales Staff / Billing Staff,Delivery Staff")]
public class NotificationsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 10;
        var cutoff = DateTime.Today.AddDays(30);
        var query = db.Inventories.AsNoTracking().Where(i => !i.IsArchived)
            .Select(i => new InventoryNotification
            {
                ProductId = i.ProductId,
                ProductName = i.Product != null ? i.Product.ProductName : "Product unavailable",
                StockQuantity = i.StockQuantity,
                ReorderLevel = i.ReorderLevel,
                LowStock = i.StockQuantity <= i.ReorderLevel,
                Expiring = (i.ExpirationDate.HasValue && i.ExpirationDate.Value <= cutoff) ||
                    db.InventoryBatches.Any(b => b.ProductId == i.ProductId && b.Quantity > 0 &&
                        b.ExpirationDate.HasValue && b.ExpirationDate.Value <= cutoff),
                Quarantined = db.InventoryBatches.Any(b => b.ProductId == i.ProductId && b.Quantity > 0 && b.IsQuarantined),
                SpoiledQuantity = i.SpoiledQuantity,
                DamagedQuantity = i.DamagedQuantity
            })
            .Where(i => i.LowStock || i.Expiring || i.Quarantined || i.SpoiledQuantity > 0 || i.DamagedQuantity > 0);

        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = total;
        return View(await query.OrderBy(i => i.ProductName).ThenBy(i => i.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }
}
