using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
[Authorize(Roles = "Main Admin,Warehouse Staff")]
public class CategoriesController : Controller
{
private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Categories
    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Where(c => !c.IsArchived)
            .ToListAsync();

        return View(categories);
    }

    // GET: Categories/Archived
    public async Task<IActionResult> Archived()
    {
        var categories = await _context.Categories
            .Where(c => c.IsArchived)
            .ToListAsync();

        return View(categories);
    }

    // GET: Categories/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return NotFound();
        }

        return View(category);
    }

    // GET: Categories/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Categories/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (ModelState.IsValid)
        {
            category.IsArchived = false;
            category.Status = "Active";

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(category);
    }

    // GET: Categories/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                !c.IsArchived);

        if (category == null)
        {
            return NotFound();
        }

        return View(category);
    }

    // POST: Categories/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id)
        {
            return NotFound();
        }

        var existingCategory = await _context.Categories
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                !c.IsArchived);

        if (existingCategory == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                existingCategory.CategoryCode = category.CategoryCode;
                existingCategory.CategoryName = category.CategoryName;
                existingCategory.Description = category.Description;
                existingCategory.Status = category.Status;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(category.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(category);
    }

    // GET: Categories/Archive/5
    public async Task<IActionResult> Archive(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                !c.IsArchived);

        if (category == null)
        {
            return NotFound();
        }

        // Check if active products use this category
        var hasActiveProducts = await _context.Products
            .AnyAsync(p =>
                p.CategoryId == category.Id &&
                !p.IsArchived);

        if (hasActiveProducts)
        {
            TempData["ErrorMessage"] =
                "This category cannot be archived because it has active products. " +
                "Please archive or change the category of those products first.";

            return RedirectToAction(nameof(Index));
        }

        return View(category);
    }

    // POST: Categories/Archive/5
    [HttpPost, ActionName("Archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveConfirmed(int id)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                !c.IsArchived);

        if (category == null)
        {
            return NotFound();
        }

        // Do not archive if active products still use it
        var hasActiveProducts = await _context.Products
            .AnyAsync(p =>
                p.CategoryId == category.Id &&
                !p.IsArchived);

        if (hasActiveProducts)
        {
            TempData["ErrorMessage"] =
                "This category cannot be archived because it has active products. " +
                "Please archive or change the category of those products first.";

            return RedirectToAction(nameof(Index));
        }

        // Soft archive
        category.IsArchived = true;
        category.Status = "Archived";

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST: Categories/Restore/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.IsArchived);

        if (category == null)
        {
            return NotFound();
        }

        category.IsArchived = false;
        category.Status = "Active";

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Archived));
    }

    private bool CategoryExists(int id)
    {
        return _context.Categories.Any(c => c.Id == id);
    }
}

}
