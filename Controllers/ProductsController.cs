using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsArchived)
                .ToListAsync();

            return View(products);
        }

        // GET: Products/Archived
        public async Task<IActionResult> Archived()
        {
            var products = await _context.Products
                .Include(p => p.Category)
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
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        public async Task<IActionResult> Create()
        {
            await LoadCategories();

            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            if (ModelState.IsValid)
            {
                product.IsArchived = false;
                product.Status = "Active";

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            await LoadCategories(product.CategoryId);

            return View(product);
        }

        // GET: Products/Edit/5
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

            await LoadCategories(product.CategoryId);

            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
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

                    existingProduct.Unit = product.Unit;
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

            await LoadCategories(product.CategoryId);

            return View(product);
        }

        // GET: Products/Archive/5
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
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

        // Load active categories for Create/Edit dropdown
private async Task LoadCategories(int? selectedCategoryId = null)
{
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

    // TEMPORARY: check how many categories ASP.NET finds
    ViewBag.CategoryCount = categories.Count;
}

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
