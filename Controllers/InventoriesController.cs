using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
    public class InventoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Inventories
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Inventories.Include(i => i.Product).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => i.Product != null &&
                    (i.Product.ProductName.Contains(search) || i.Product.ProductCode.Contains(search)));

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = await query.CountAsync();
            var inventories = await query.OrderBy(i => i.Product!.ProductName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return View(inventories);
        }

        public async Task<IActionResult> Alerts()
        {
            var today = DateTime.Today;
            var alerts = await _context.Inventories
                .Include(i => i.Product)
                .Where(i => i.StockQuantity <= i.ReorderLevel ||
                    (i.ExpirationDate.HasValue && i.ExpirationDate.Value.Date <= today.AddDays(30)) ||
                    i.SpoiledQuantity > 0 || i.DamagedQuantity > 0)
                .OrderBy(i => i.ExpirationDate)
                .ToListAsync();

            return View(alerts);
        }

        // GET: Inventories/Details/5
        public async Task<IActionResult> Details(int? id)
{
      if (id == null)
    {
        return NotFound();
    }

    var inventory = await _context.Inventories
        .Include(i => i.Product)
        .FirstOrDefaultAsync(i => i.Id == id);

    if (inventory == null)
    {
        return NotFound();
    }

    return View(inventory);
}

        // GET: Inventories/Create
        public IActionResult Create()
        {
            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View();
        }

        // POST: Inventories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Inventory inventory)
        {
            if (ModelState.IsValid)
            {
                inventory.LastUpdated = DateTime.Now;
                inventory.ExpirationDate = inventory.ExpirationDate?.Date;

                if (inventory.StockQuantity <= 0)
                {
                    inventory.StockStatus = "Out of Stock";
                }
                else if (inventory.StockQuantity <= inventory.ReorderLevel)
                {
                    inventory.StockStatus = "Low Stock";
                }
                else
                {
                    inventory.StockStatus = "In Stock";
                }

                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View(inventory);
        }

        // GET: Inventories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
        if (id == null)
    {
        return NotFound();
    }

        var inventory = await _context.Inventories.FindAsync(id);

        if (inventory == null)
     {
        return NotFound();
     }

     ViewBag.Products = _context.Products
        .Where(p => p.Status == "Active")
        .ToList();

    return View(inventory);
}
        // POST: Inventories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    inventory.LastUpdated = DateTime.Now;
                    inventory.ExpirationDate = inventory.ExpirationDate?.Date;

                    if (inventory.StockQuantity <= 0)
                    {
                        inventory.StockStatus = "Out of Stock";
                    }
                    else if (inventory.StockQuantity <= inventory.ReorderLevel)
                    {
                        inventory.StockStatus = "Low Stock";
                    }
                    else
                    {
                        inventory.StockStatus = "In Stock";
                    }

                    _context.Update(inventory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InventoryExists(inventory.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View(inventory);
        }

        // GET: Inventories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventory == null)
            {
                return NotFound();
            }

            return View(inventory);
        }

        // POST: Inventories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var inventory = await _context.Inventories.FindAsync(id);

            if (inventory != null)
            {
                _context.Inventories.Remove(inventory);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool InventoryExists(int id)
        {
            return _context.Inventories.Any(i => i.Id == id);
        }
    }
}