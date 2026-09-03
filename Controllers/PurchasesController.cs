
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
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
        public IActionResult Create()
        {
            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToList();

            return View();
        }

        // POST: Purchases/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Purchase purchase)
        {
            if (ModelState.IsValid)
            {
                purchase.TotalAmount = 0;
                purchase.Status = "Pending";
                purchase.IsArchived = false;

                _context.Purchases.Add(purchase);

                await _context.SaveChangesAsync();

                return RedirectToAction(
                    nameof(Details),
                    new { id = purchase.Id });
            }

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToList();

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

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToList();

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

            if (ModelState.IsValid)
            {
                try
                {
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

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.Status == "Active")
                .ToList();

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
            // Make sure this is a new PurchaseItem
            // and let MySQL generate the primary key
            item.Id = 0;

            if (ModelState.IsValid)
            {
                // Calculate subtotal
                item.Subtotal = item.Quantity * item.UnitPrice;

                _context.PurchaseItems.Add(item);

                await _context.SaveChangesAsync();

                // Recalculate purchase total
                var purchase = await _context.Purchases
                    .Include(p => p.PurchaseItems)
                    .FirstOrDefaultAsync(p => p.Id == item.PurchaseId);

                if (purchase != null)
                {
                    purchase.TotalAmount = purchase.PurchaseItems
                        .Sum(i => i.Subtotal);

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(
                    nameof(Details),
                    new { id = item.PurchaseId });
            }

            // Reload products if validation fails
            ViewBag.Products = await _context.Products
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Purchase = await _context.Purchases
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == item.PurchaseId);

            return View(item);
        }

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
        // Shows archived purchases
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