using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
[Authorize(Roles = "Main Admin,Purchasing/Supplier Staff")]
public class SuppliersController : Controller
{
private readonly ApplicationDbContext _context;

    public SuppliersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Suppliers
    public async Task<IActionResult> Index()
    {
        var suppliers = await _context.Suppliers.ToListAsync();
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
