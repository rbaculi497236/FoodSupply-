using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
[Authorize(Roles = "Admin,Manager,Main Admin,Purchasing/Supplier Staff")]
public class SuppliersController : Controller
{
private readonly ApplicationDbContext _context;

    public SuppliersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Suppliers
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        const int pageSize = 10;
        var query = _context.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.SupplierCode.Contains(search) || s.SupplierName.Contains(search) ||
                (s.PhoneNumber != null && s.PhoneNumber.Contains(search)));
        ViewBag.Search = search; ViewBag.Page = page; ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = await query.CountAsync();
        var suppliers = await query.OrderBy(s => s.SupplierName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(suppliers);
    }

    // GET: Suppliers/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id);

        if (supplier == null)
        {
            return NotFound();
        }

        return View(supplier);
    }

    // GET: Suppliers/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Suppliers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        var nextId = (await _context.Suppliers
            .Select(s => (int?)s.Id)
            .MaxAsync() ?? 0) + 1;
        supplier.SupplierCode = $"SUP-{nextId:D6}";
        ModelState.Remove(nameof(Supplier.SupplierCode));

        if (ModelState.IsValid)
        {
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(supplier);
    }

    // GET: Suppliers/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var supplier = await _context.Suppliers.FindAsync(id);

        if (supplier == null)
        {
            return NotFound();
        }

        return View(supplier);
    }

    // POST: Suppliers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier)
    {
        if (id != supplier.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(supplier);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupplierExists(supplier.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(supplier);
    }

    // GET: Suppliers/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id);

        if (supplier == null)
        {
            return NotFound();
        }

        return View(supplier);
    }

    // POST: Suppliers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);

        if (supplier != null)
        {
            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool SupplierExists(int id)
    {
        return _context.Suppliers.Any(s => s.Id == id);
    }
}

}
