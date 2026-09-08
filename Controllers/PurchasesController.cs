using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Main Admin,Purchasing/Supplier Staff")]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchasesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Purchases
        // Shows only active purchases
        public async Task<IActionResult> Index()
        {
            var purchases = await _context.Purchases
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            return View(purchases);
        }

        // GET: Purchases/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseItems)
                    .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            return View(purchase);
        }

        // GET: Purchases/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = await _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            return View();
        }

        // POST: Purchases/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Purchase purchase)
        {
            if (ModelState.IsValid)
            {
                // New purchases always start as Pending.
                // Inventory will NOT increase yet.
                purchase.Status = "Pending";
                purchase.IsArchived = false;

                foreach (var item in purchase.PurchaseItems)
                {
                    item.Id = 0;
                    item.Subtotal = item.Quantity * item.UnitPrice;
                }

                purchase.TotalAmount = purchase.PurchaseItems
                    .Sum(item => item.Subtotal);

                _context.Purchases.Add(purchase);

                await _context.SaveChangesAsync();

                return RedirectToAction(
                    nameof(Details),
                    new { id = purchase.Id });
            }

            ViewBag.Suppliers = await _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            return View(purchase);
        }

        // GET: Purchases/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var purchase = await _context.Purchases
                .FindAsync(id);

            if (purchase == null)
            {
                return NotFound();
            }

            // Do not allow editing a received purchase.
            if (purchase.Status == "Received")
            {
                TempData["Error"] =
                    "A received purchase cannot be edited.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = purchase.Id });
            }

            ViewBag.Suppliers = await _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToListAsync();

            return View(purchase);
        }

        // POST: Purchases/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Purchase purchase)
        {
            if (id != purchase.Id)
            {
                return NotFound();
            }

            // Prevent editing a purchase that has already been received.
            var existingPurchase = await _context.Purchases
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingPurchase == null)
            {
                return NotFound();
            }

            if (existingPurchase.Status == "Received")
            {
                TempData["Error"] =
                    "A received purchase cannot be edited.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Keep the existing status.
                    purchase.Status = existingPurchase.Status;
                    purchase.IsArchived = existingPurchase.IsArchived;

                    _context.Update(purchase);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PurchaseExists(purchase.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Suppliers = await _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToListAsync();

            return View(purchase);
        }

        // GET: Purchases/AddItem/5
        public async Task<IActionResult> AddItem(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            // Do not allow items to be added after receiving.
            if (purchase.Status == "Received")
            {
                TempData["Error"] =
                    "Items cannot be added to a received purchase.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            ViewBag.Products = await _context.Products
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Purchase = purchase;

            var item = new PurchaseItem
            {
                PurchaseId = purchase.Id,
                Quantity = 1
            };

            return View(item);
        }

        // POST: Purchases/AddItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(PurchaseItem item)
        {
            item.Id = 0;

            // Make sure the purchase exists.
            var purchase = await _context.Purchases
                .FirstOrDefaultAsync(p => p.Id == item.PurchaseId);

            if (purchase == null)
            {
                return NotFound();
            }

            // Do not allow items after receiving.
            if (purchase.Status == "Received")
            {
                TempData["Error"] =
                    "Items cannot be added to a received purchase.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = item.PurchaseId });
            }

            if (ModelState.IsValid)
            {
                item.Subtotal = item.Quantity * item.UnitPrice;

                _context.PurchaseItems.Add(item);

                await _context.SaveChangesAsync();

                // Recalculate purchase total.
                purchase.TotalAmount = await _context.PurchaseItems
                    .Where(i => i.PurchaseId == purchase.Id)
                    .SumAsync(i => i.Subtotal);

                await _context.SaveChangesAsync();

                return RedirectToAction(
                    nameof(Details),
                    new { id = item.PurchaseId });
            }

            ViewBag.Products = await _context.Products
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == item.PurchaseId);

            return View(item);
        }

        // =========================================================
        // RECEIVE PURCHASE
        // =========================================================

        // POST: Purchases/Receive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(int id)
        {
            // Load purchase and its items.
            var purchase = await _context.Purchases
                .Include(p => p.PurchaseItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
            {
                return NotFound();
            }

            // Prevent receiving an already received purchase.
            if (purchase.Status == "Received")
            {
                TempData["Error"] =
                    "This purchase has already been received.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            // A purchase without items cannot be received.
            if (!purchase.PurchaseItems.Any())
            {
                TempData["Error"] =
                    "This purchase cannot be received because it has no items.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            // Process every purchased item.
            foreach (var purchaseItem in purchase.PurchaseItems)
            {
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i =>
                        i.ProductId == purchaseItem.ProductId);

                if (inventory == null)
                {
                    // Create inventory record if one doesn't exist.
                    inventory = new Inventory
                    {
                        ProductId = purchaseItem.ProductId,
                        StockQuantity = purchaseItem.Quantity,
                        ReorderLevel = 0,
                        LastUpdated = DateTime.Now
                    };

                    UpdateStockStatus(inventory);

                    _context.Inventories.Add(inventory);
                }
                else
                {
                    // Increase existing stock.
                    inventory.StockQuantity += purchaseItem.Quantity;
                    inventory.LastUpdated = DateTime.Now;

                    UpdateStockStatus(inventory);
                }
            }

            // Mark purchase as received.
            purchase.Status = "Received";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Purchase received successfully. Inventory has been updated.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        // Updates Inventory.StockStatus based on quantity.
        private void UpdateStockStatus(Inventory inventory)
        {
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
        }

        // =========================================================
        // ARCHIVE
        // =========================================================

        // POST: Purchases/Archive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var purchase = await _context.Purchases
                .FindAsync(id);

            if (purchase == null)
            {
                return NotFound();
            }

            purchase.IsArchived = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Purchases/Archived
        public async Task<IActionResult> Archived()
        {
            var purchases = await _context.Purchases
                .Include(p => p.Supplier)
                .Where(p => p.IsArchived)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            return View(purchases);
        }

        // POST: Purchases/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var purchase = await _context.Purchases
                .FindAsync(id);

            if (purchase == null)
            {
                return NotFound();
            }

            purchase.IsArchived = false;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Archived));
        }

        private bool PurchaseExists(int id)
        {
            return _context.Purchases
                .Any(p => p.Id == id);
        }
    }
}