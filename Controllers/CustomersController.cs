using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff")]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // GET: Customers
        // =========================================================
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;

            var query = _context.Customers
                .Where(c => !c.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.CustomerCode.Contains(search) ||
                    c.CustomerName.Contains(search) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            var customers = await query
                .OrderBy(c => c.CustomerName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(customers);
        }


        // =========================================================
        // GET: Customers/Archived
        // =========================================================
        public async Task<IActionResult> Archived(int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Customers.Where(c => c.IsArchived);
            var totalItems = await query.CountAsync();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
            ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
            var customers = await query.OrderBy(c => c.CustomerName)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return View(customers);
        }


        // =========================================================
        // POST: Customers/Restore/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id && c.IsArchived);

            if (customer == null)
            {
                return NotFound();
            }

            customer.IsArchived = false;
            customer.Status = "Active";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Customer {customer.CustomerName} restored successfully.";

            return RedirectToAction(nameof(Archived));
        }


        // =========================================================
        // GET: Customers/Details/5
        // =========================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            ViewBag.Activities = await _context.Set<CustomerActivity>()
                .Where(a => a.CustomerId == customer.Id)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            ViewBag.SalesOrders = await _context.SalesOrders
                .Where(s =>
                    s.CustomerId == customer.Id &&
                    !s.IsArchived)
                .OrderByDescending(s => s.OrderDate)
                .ToListAsync();

            ViewBag.Billings = await _context.Billings
                .Include(b => b.SalesOrder)
                .Where(b =>
                    b.SalesOrder != null &&
                    b.SalesOrder.CustomerId == customer.Id &&
                    !b.IsArchived)
                .OrderByDescending(b => b.InvoiceDate)
                .ToListAsync();

            ViewBag.Deliveries = await _context.Deliveries
                .Include(d => d.SalesOrder)
                .Where(d =>
                    d.SalesOrder != null &&
                    d.SalesOrder.CustomerId == customer.Id &&
                    !d.IsArchived)
                .OrderByDescending(d => d.DeliveryDate)
                .ToListAsync();

            return View(customer);
        }


        // =========================================================
        // GET: Customers/Crm
        // =========================================================
        public async Task<IActionResult> Crm()
        {
            var today = DateTime.Today;

            ViewBag.FollowUps = await _context.Customers
                .Where(c =>
                    !c.IsArchived &&
                    c.NextFollowUpDate.HasValue)
                .OrderBy(c => c.NextFollowUpDate)
                .ToListAsync();

            ViewBag.DueFollowUps = await _context.Customers
                .CountAsync(c =>
                    !c.IsArchived &&
                    c.NextFollowUpDate.HasValue &&
                    c.NextFollowUpDate.Value.Date <= today);

            ViewBag.RecentActivities = await _context.Set<CustomerActivity>()
                .Include(a => a.Customer)
                .Where(a =>
                    a.Customer != null &&
                    !a.Customer.IsArchived)
                .OrderByDescending(a => a.ActivityDate)
                .Take(10)
                .ToListAsync();

            return View();
        }


        // =========================================================
        // POST: Customers/AddActivity
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddActivity(CustomerActivity activity)
        {
            ModelState.Remove(nameof(CustomerActivity.Customer));

            if (!ModelState.IsValid)
            {
                return RedirectToAction(
                    nameof(Details),
                    new { id = activity.CustomerId });
            }

            activity.ActivityDate = DateTime.Now;

            _context.Set<CustomerActivity>().Add(activity);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = activity.CustomerId });
        }


        // =========================================================
        // GET: Customers/Create
        // =========================================================
        public IActionResult Create()
        {
            return View();
        }


        // =========================================================
        // POST: Customers/Create
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            var nextId = (await _context.Customers
                .Select(c => (int?)c.Id)
                .MaxAsync() ?? 0) + 1;

            customer.CustomerCode = $"CUST-{nextId:D6}";

            ModelState.Remove(nameof(Customer.CustomerCode));

            if (ModelState.IsValid)
            {
                _context.Customers.Add(customer);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }


        // =========================================================
        // GET: Customers/Edit/5
        // =========================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    !c.IsArchived);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }


        // =========================================================
        // POST: Customers/Edit/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(customer);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.Id))
                    {
                        return NotFound();
                    }

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }


        // =========================================================
        // GET: Customers/Archive/5
        // =========================================================
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    !c.IsArchived);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }


        // =========================================================
        // POST: Customers/ArchiveConfirmed/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    !c.IsArchived);

            if (customer == null)
            {
                return NotFound();
            }

            // Soft archive
            customer.IsArchived = true;
            customer.Status = "Archived";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Customer {customer.CustomerName} archived successfully.";

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // CHECK CUSTOMER EXISTS
        // =========================================================
        private bool CustomerExists(int id)
        {
            return _context.Customers
                .Any(c => c.Id == id);
        }
    }
}
