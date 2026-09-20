using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Admin,Manager,Main Admin,Purchasing/Supplier Staff")]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchasesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Purchases
        // Shows only active purchases
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Purchases
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.PurchaseOrderNumber.Contains(search) ||
                    (p.Supplier != null && p.Supplier.SupplierName.Contains(search)) || p.Status.Contains(search));
            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            var purchases = await query.OrderByDescending(p => p.PurchaseDate)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

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
        public async Task<IActionResult> Create([Bind("SupplierId,PurchaseDate,Notes,PurchaseItems")] Purchase purchase)
        {
            ModelState.Remove(nameof(Purchase.PurchaseOrderNumber));

            if (ModelState.IsValid)
            {
                // New purchases always start as Pending.
                // Inventory will NOT increase yet.
                purchase.PurchaseOrderNumber = "PENDING";
                purchase.Status = "Pending";
                purchase.IsArchived = false;

                foreach (var item in purchase.PurchaseItems)
                {
                    item.Id = 0; item.Product = null; item.Purchase = null;
                    item.Subtotal = item.Quantity * item.UnitPrice;
                }

                purchase.TotalAmount = purchase.PurchaseItems
                    .Sum(item => item.Subtotal);

                _context.Purchases.Add(purchase);

                await _context.SaveChangesAsync();

                purchase.PurchaseOrderNumber = $"PO-{purchase.Id:D6}";
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
            if (purchase.Status != "Pending")
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

            if (existingPurchase.Status != "Pending")
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

                                        var tracked = await _context.Purchases.FindAsync(id);
                    if (tracked == null) return NotFound();
                    tracked.SupplierId = purchase.SupplierId;
                    tracked.PurchaseDate = purchase.PurchaseDate;
                    tracked.Notes = purchase.Notes;

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
            if (purchase.Status != "Pending")
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
            item.Id = 0; item.Product = null; item.Purchase = null;

            // Make sure the purchase exists.
            var purchase = await _context.Purchases
                .FirstOrDefaultAsync(p => p.Id == item.PurchaseId);

            if (purchase == null)
            {
                return NotFound();
            }

            // Do not allow items after receiving.
            if (purchase.Status != "Pending")
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

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Receive(int id) => RedirectToAction("Receive", "Operations", new { id });
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
        public async Task<IActionResult> Archived(int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Purchases
                .Include(p => p.Supplier)
                .Where(p => p.IsArchived);
            var totalItems = await query.CountAsync();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
            ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
            var purchases = await query.OrderByDescending(p => p.PurchaseDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
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
