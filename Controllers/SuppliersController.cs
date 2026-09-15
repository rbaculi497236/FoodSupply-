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

        // =========================================================
        // GET: Suppliers
        // =========================================================
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;

            var query = _context.Suppliers
                .Where(s => !s.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s =>
                    s.SupplierCode.Contains(search) ||
                    s.SupplierName.Contains(search) ||
                    (s.PhoneNumber != null &&
                     s.PhoneNumber.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            var suppliers = await query
                .OrderBy(s => s.SupplierName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(suppliers);
        }

        // =========================================================
        // GET: Suppliers/Archived
        // =========================================================
        public async Task<IActionResult> Archived()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsArchived)
                .OrderBy(s => s.SupplierName)
                .ToListAsync();

            return View(suppliers);
        }

        // =========================================================
        // POST: Suppliers/Restore/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.IsArchived);

            if (supplier == null)
            {
                return NotFound();
            }

            supplier.IsArchived = false;
            supplier.Status = "Active";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Supplier restored successfully.";

            return RedirectToAction(nameof(Archived));
        }

        // =========================================================
        // GET: Suppliers/Details/5
        // =========================================================
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

        // =========================================================
        // GET: Suppliers/Create
        // =========================================================
        public IActionResult Create()
        {
            return View();
        }

        // =========================================================
        // POST: Suppliers/Create
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            var nextId = (await _context.Suppliers
                .Select(s => (int?)s.Id)
                .MaxAsync() ?? 0) + 1;

            supplier.SupplierCode = $"SUP-{nextId:D6}";

            // New suppliers are active by default
            supplier.IsArchived = false;

            if (string.IsNullOrWhiteSpace(supplier.Status))
            {
                supplier.Status = "Active";
            }

            ModelState.Remove(nameof(Supplier.SupplierCode));

            if (ModelState.IsValid)
            {
                _context.Suppliers.Add(supplier);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Supplier added successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        // =========================================================
        // GET: Suppliers/Edit/5
        // =========================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsArchived);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // =========================================================
        // POST: Suppliers/Edit/5
        // =========================================================
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
                    // Keep the existing archive status unchanged
                    var existingSupplier = await _context.Suppliers
                        .FirstOrDefaultAsync(s => s.Id == id);

                    if (existingSupplier == null)
                    {
                        return NotFound();
                    }

                    existingSupplier.SupplierCode = supplier.SupplierCode;
                    existingSupplier.SupplierName = supplier.SupplierName;
                    existingSupplier.ContactPerson = supplier.ContactPerson;
                    existingSupplier.PhoneNumber = supplier.PhoneNumber;
                    existingSupplier.Email = supplier.Email;
                    existingSupplier.Address = supplier.Address;
                    existingSupplier.Status = supplier.Status;

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

                TempData["SuccessMessage"] =
                    "Supplier updated successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        // =========================================================
        // GET: Suppliers/Archive/5
        // =========================================================
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsArchived);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // =========================================================
        // POST: Suppliers/ArchiveConfirmed/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsArchived);

            if (supplier == null)
            {
                return NotFound();
            }

            supplier.IsArchived = true;
            supplier.Status = "Archived";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Supplier archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // Check if Supplier Exists
        // =========================================================
        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(s => s.Id == id);
        }
    }
}
