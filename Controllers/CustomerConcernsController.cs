using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    [Authorize]
    public class CustomerConcernsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomerConcernsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CustomerConcerns
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;

            var query = _context.CustomerConcerns
                .Include(c => c.Customer)
                .Where(c => !c.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.ConcernType.Contains(search) ||
                    c.Subject.Contains(search) ||
                    c.Status.Contains(search) ||
                    c.Customer != null && c.Customer.CustomerName.Contains(search));
            }

            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            var concerns = await query
                .OrderByDescending(c => c.DateReported)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(concerns);
        }

        // GET: CustomerConcerns/Archived
        public async Task<IActionResult> Archived()
        {
            var concerns = await _context.CustomerConcerns
                .Include(c => c.Customer)
                .Where(c => c.IsArchived)
                .OrderByDescending(c => c.DateReported)
                .ToListAsync();

            return View(concerns);
        }

        // GET: CustomerConcerns/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var concern = await _context.CustomerConcerns
                .Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (concern == null)
                return NotFound();

            return View(concern);
        }

        // GET: CustomerConcerns/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Customers
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            return View();
        }

        // POST: CustomerConcerns/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerConcern concern)
        {
            if (ModelState.IsValid)
            {
                concern.DateReported = DateTime.Now;
                concern.Status = "Pending";
                concern.IsArchived = false;

                _context.CustomerConcerns.Add(concern);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            return View(concern);
        }

        // GET: CustomerConcerns/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var concern = await _context.CustomerConcerns.FindAsync(id);

            if (concern == null)
                return NotFound();

            ViewBag.Customers = await _context.Customers
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            return View(concern);
        }

        // POST: CustomerConcerns/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerConcern concern)
        {
            if (id != concern.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                if (concern.Status == "Resolved" && concern.ResolvedDate == null)
                {
                    concern.ResolvedDate = DateTime.Now;
                }

                _context.Update(concern);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            return View(concern);
        }

        // Archive instead of delete
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
                return NotFound();

            var concern = await _context.CustomerConcerns.FindAsync(id);

            if (concern == null)
                return NotFound();

            concern.IsArchived = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Restore archived concern
        public async Task<IActionResult> Restore(int? id)
        {
            if (id == null)
                return NotFound();

            var concern = await _context.CustomerConcerns.FindAsync(id);

            if (concern == null)
                return NotFound();

            concern.IsArchived = false;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Archived));
        }
    }
}
