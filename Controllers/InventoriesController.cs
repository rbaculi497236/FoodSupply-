using FoodSupply.Services;
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

        // =========================================================
        // GET: Inventories
        // =========================================================
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;

            var query = _context.Inventories
                .Where(i => !i.IsArchived)
                .Include(i => i.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(i =>
                    i.Product != null &&
                    (
                        i.Product.ProductName.Contains(search) ||
                        i.Product.ProductCode.Contains(search)
                    ));
            }

            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            var inventories = await query
                .OrderBy(i => i.Product!.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(inventories);
        }


        // =========================================================
        // GET: Inventories/Alerts
        // =========================================================
        public async Task<IActionResult> Alerts(int page = 1)
        {
            const int pageSize = 10;
            var today = DateTime.Today;

            var query = _context.Inventories
                .Include(i => i.Product)
                .Where(i =>
                    !i.IsArchived &&
                    (
                        i.StockQuantity <= i.ReorderLevel || _context.InventoryBatches.Any(b => b.ProductId == i.ProductId && b.Quantity > 0 && (b.IsQuarantined || (b.ExpirationDate.HasValue && b.ExpirationDate.Value <= today.AddDays(30)))) ||
                        (
                            i.ExpirationDate.HasValue &&
                            i.ExpirationDate.Value.Date <= today.AddDays(30)
                        ) ||
                        i.SpoiledQuantity > 0 ||
                        i.DamagedQuantity > 0
                ));
            var totalItems = await query.CountAsync();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
            ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
            var alerts = await query.OrderBy(i => i.ExpirationDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return View(alerts);
        }


        // =========================================================
        // GET: Inventories/Archived
        // =========================================================
        public async Task<IActionResult> Archived(int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Inventories
                .Where(i => i.IsArchived)
                .Include(i => i.Product);
            var totalItems = await query.CountAsync();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
            ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
            var inventories = await query.OrderByDescending(i => i.LastUpdated)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return View(inventories);
        }


        // =========================================================
        // POST: Inventories/Restore/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.IsArchived);

            if (inventory == null)
            {
                return NotFound();
            }

            inventory.IsArchived = false;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Inventory record restored successfully.";

            return RedirectToAction(nameof(Archived));
        }


        // =========================================================
        // GET: Inventories/Details/5
        // =========================================================
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


        // =========================================================
        // GET: Inventories/Create
        // =========================================================
        public IActionResult Create()
        {
            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View();
        }


        // =========================================================
        // POST: Inventories/Create
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Inventory inventory)
        {
            if (await _context.Inventories.AnyAsync(i => i.ProductId == inventory.ProductId))
                ModelState.AddModelError("ProductId", "This product already has inventory. Open its batches to receive or adjust stock.");
            if (inventory.StockQuantity != 0)
                ModelState.AddModelError("StockQuantity", "Create the inventory with zero stock, then receive a batch from its details page.");
            if (ModelState.IsValid)
            {
                inventory.LastUpdated = DateTime.Now;

                inventory.ExpirationDate =
                    inventory.ExpirationDate?.Date;

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

                inventory.IsArchived = false;

                _context.Inventories.Add(inventory);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View(inventory);
        }


        // =========================================================
        // GET: Inventories/Edit/5
        // =========================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    !i.IsArchived);

            if (inventory == null)
            {
                return NotFound();
            }

            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View(inventory);
        }


        // =========================================================
        // POST: Inventories/Edit/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return NotFound();
            }

            var existing = await _context.Inventories.SingleOrDefaultAsync(i => i.Id == id && !i.IsArchived);
            if (existing == null) return NotFound();
            if (inventory.StockQuantity != existing.StockQuantity || inventory.ProductId != existing.ProductId)
                ModelState.AddModelError("", "Use batch adjustments to change stock. The product cannot be changed.");
            if (ModelState.IsValid)
            {
                existing.ReorderLevel = inventory.ReorderLevel;
                existing.StockStatus = existing.StockQuantity == 0 ? "Out of Stock" : existing.StockQuantity <= existing.ReorderLevel ? "Low Stock" : "In Stock";
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Products = _context.Products
                .Where(p => p.Status == "Active")
                .ToList();

            return View(inventory);
        }


        // =========================================================
        // GET: Inventories/Archive/5
        // =========================================================
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    !i.IsArchived);

            if (inventory == null)
            {
                return NotFound();
            }

            return View(inventory);
        }


        // =========================================================
        // POST: Inventories/ArchiveConfirmed/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    !i.IsArchived);

            if (inventory == null)
            {
                return NotFound();
            }

            // Soft archive
            inventory.IsArchived = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Inventory record archived successfully.";

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // CHECK INVENTORY EXISTS
        // =========================================================
        private bool InventoryExists(int id)
        {
            return _context.Inventories
                .Any(i => i.Id == id);
        }
    }
}
