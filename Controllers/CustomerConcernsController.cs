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
	public IActionResult Index()
 	{
    	   return Content("Customer Concerns Controller is working!");
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