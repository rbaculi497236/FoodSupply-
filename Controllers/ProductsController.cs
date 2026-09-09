using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff,Sales Staff / Billing Staff,Sales/Customer Staff")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Products
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => !p.IsArchived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.ProductCode.Contains(search) ||
                    p.ProductName.Contains(search) ||
                    p.Unit.Contains(search));
            }

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = await query.CountAsync();

            var products = await query
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(products);
        }

        // GET: Products/Archived
        public async Task<IActionResult> Archived()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.IsArchived)
                .ToListAsync();

            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> Create(Product product)
        {
            var nextId = (await _context.Products
                .Select(p => (int?)p.Id)
                .MaxAsync() ?? 0) + 1;

            product.ProductCode = $"PROD-{nextId:D6}";

            // ProductCode is generated automatically
            ModelState.Remove(nameof(Product.ProductCode));

            if (ModelState.IsValid)
            {
                product.IsArchived = false;
                product.Status = "Active";

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            // Reload dropdowns if validation fails
            await LoadDropdowns(product.CategoryId, product.SupplierId);

            return View(product);
        }

        // GET: Products/Edit/5
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);

            if (product == null)
            {
                return NotFound();
            }

            await LoadDropdowns(product.CategoryId, product.SupplierId);

            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);

            if (existingProduct == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingProduct.ProductCode = product.ProductCode;
                    existingProduct.ProductName = product.ProductName;
                    existingProduct.Description = product.Description;

                    // Save selected category
                    existingProduct.CategoryId = product.CategoryId;

                    // Save selected supplier
                    existingProduct.SupplierId = product.SupplierId;

                    existingProduct.Unit = product.Unit;
                    existingProduct.Boxes = product.Boxes;
                    existingProduct.PiecesPerBox = product.PiecesPerBox;
                    existingProduct.Price = product.Price;
                    existingProduct.StockQuantity = product.StockQuantity;
                    existingProduct.ReorderLevel = product.ReorderLevel;
                    existingProduct.ExpirationDate = product.ExpirationDate;
                    existingProduct.Status = product.Status;

                    existingProduct.IsArchived = false;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            // Reload dropdowns if validation fails
            await LoadDropdowns(product.CategoryId, product.SupplierId);

            return View(product);
        }

        // GET: Products/Archive/5
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Products/Archive/5
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived);

            if (product == null)
            {
                return NotFound();
            }

            product.IsArchived = true;
            product.Status = "Archived";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: Products/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Main Admin,Warehouse Staff")]
        public async Task<IActionResult> Restore(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.IsArchived);

            if (product == null)
            {
                return NotFound();
            }

            product.IsArchived = false;
            product.Status = "Active";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Archived));
        }

        // Load Categories and Suppliers for Create/Edit
        private async Task LoadDropdowns(
            int? selectedCategoryId = null,
            int? selectedSupplierId = null)
        {
            // Categories
            var categories = await _context.Categories
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            ViewBag.CategoryId = new SelectList(
                categories,
                "Id",
                "CategoryName",
                selectedCategoryId
            );

            ViewBag.CategoryCount = categories.Count;


            // Suppliers
            var suppliers = await _context.Suppliers
                .Where(s => s.Status == "Active")
                .OrderBy(s => s.SupplierName)
                .ToListAsync();

            ViewBag.SupplierId = new SelectList(
                suppliers,
                "Id",
                "SupplierName",
                selectedSupplierId
            );

            ViewBag.SupplierCount = suppliers.Count;
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}