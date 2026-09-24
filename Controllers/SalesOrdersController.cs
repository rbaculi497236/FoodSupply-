using FoodSupply.Services;
using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff")]
    public class SalesOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SalesOrderService _orders;

        public SalesOrdersController(ApplicationDbContext context, SalesOrderService orders)
        {
            _context = context; _orders = orders;
        }

        // GET: SalesOrders
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;
            var query = _context.SalesOrders
                .Where(s => !s.IsArchived)
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.Customer != null && s.Customer.CustomerName.Contains(search) ||
                    s.Status.Contains(search) || s.Id.ToString().Contains(search));
            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            var salesOrders = await query.OrderByDescending(s => s.OrderDate)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return View(salesOrders);
        }

        // GET: SalesOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var salesOrder = await _context.SalesOrders
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (salesOrder == null)
            {
                return NotFound();
            }

            return View(salesOrder);
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int? id)
        {
            if (id == null) return NotFound();
            var order = await _context.SalesOrders.AsNoTracking()
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (order == null) return NotFound();

            return View("~/Views/Shared/Receipt.cshtml", new ReceiptViewModel
            {
                OrderId = order.Id, Controller = "SalesOrders", OrderType = "Sales Order",
                Number = $"SO-{order.Id:D6}", Date = order.OrderDate, Status = order.Status,
                PartyName = order.Customer?.CustomerName ?? "Unknown Customer",
                PartyAddress = order.Customer?.Address, Notes = order.Remarks, Total = order.TotalAmount,
                Items = order.SalesOrderItems.OrderBy(i => i.Id).Select(i => new ReceiptLine(
                    i.Quantity, i.Product?.ProductName ?? "Product unavailable", i.UnitPrice, i.Subtotal)).ToList()
            });
        }

        // GET: SalesOrders/Create
        public async Task<IActionResult> Create()
        {
            await LoadCustomers();
            await LoadProducts();

            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesOrder salesOrder)
        {
            if (ModelState.IsValid)
            {
                try { await _orders.SaveAsync(salesOrder); return RedirectToAction(nameof(Index)); }
                catch (InvalidOperationException ex) when (ex.Source == "FoodSupply.Business") { ModelState.AddModelError("", ex.Message); }
            }
            await LoadCustomers(salesOrder.CustomerId); await LoadProducts();
            return View(salesOrder);
        }
        // GET: SalesOrders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var salesOrder = await _context.SalesOrders
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (salesOrder == null)
            {
                return NotFound();
            }

            await LoadCustomers(salesOrder.CustomerId);
            await LoadProducts();

            return View(salesOrder);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SalesOrder salesOrder)
        {
            if (id != salesOrder.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try { await _orders.SaveAsync(salesOrder, id); return RedirectToAction(nameof(Index)); }
                catch (InvalidOperationException ex) when (ex.Source == "FoodSupply.Business") { ModelState.AddModelError("", ex.Message); }
            }
            await LoadCustomers(salesOrder.CustomerId); await LoadProducts();
            return View(salesOrder);
        }
        // GET: SalesOrders/Archive/5
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var salesOrder = await _context.SalesOrders
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsArchived);

            if (salesOrder == null)
            {
                return NotFound();
            }

            return View(salesOrder);
        }

        // POST: SalesOrders/ArchiveConfirmed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    !s.IsArchived);

            if (salesOrder == null)
            {
                return NotFound();
            }

            BusinessRule.Require(salesOrder.Status is "Cancelled" or "Delivered", "Complete or cancel an order before archiving it.");
            salesOrder.IsArchived = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Sales Order #{salesOrder.Id} archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: SalesOrders/Archived
        public async Task<IActionResult> Archived(int page = 1)
        {
            const int pageSize = 10;
            var query = _context.SalesOrders
                .Where(s => s.IsArchived)
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product);
            var totalItems = await query.CountAsync();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
            ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
            var archivedOrders = await query.OrderByDescending(s => s.OrderDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return View(archivedOrders);
        }

        // POST: SalesOrders/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.IsArchived);

            if (salesOrder == null)
            {
                return NotFound();
            }

            salesOrder.IsArchived = false;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Sales Order #{salesOrder.Id} restored successfully.";

            return RedirectToAction(nameof(Archived));
        }

        // Load active customers
        private async Task LoadCustomers(
            int? selectedCustomerId = null)
        {
            var customers = await _context.Customers
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            ViewBag.CustomerId =
                new SelectList(
                    customers,
                    "Id",
                    "CustomerName",
                    selectedCustomerId
                );
        }

        // Load active products
        private async Task LoadProducts()
        {
            var products = await _context.Products.AsNoTracking()
                .Where(p => p.Status == "Active" && !p.IsArchived)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            var today = DateTime.Today;
            var usableStock = await _context.InventoryBatches
                .Where(b => !b.IsQuarantined && (!b.ExpirationDate.HasValue || b.ExpirationDate > today) &&
                    _context.Inventories.Any(i => i.ProductId == b.ProductId && !i.IsArchived))
                .GroupBy(b => b.ProductId).Select(g => new { ProductId = g.Key, Quantity = g.Sum(b => b.Quantity) })
                .ToDictionaryAsync(b => b.ProductId, b => b.Quantity);
            foreach (var product in products) product.StockQuantity = usableStock.GetValueOrDefault(product.Id);

            ViewBag.Products = products;
        }

        // Update inventory status
        private void UpdateInventoryStatus(
            Inventory inventory)
        {
            if (inventory.StockQuantity <= 0)
            {
                inventory.StockStatus =
                    "Out of Stock";
            }
            else if (
                inventory.StockQuantity
                <= inventory.ReorderLevel)
            {
                inventory.StockStatus =
                    "Low Stock";
            }
            else
            {
                inventory.StockStatus =
                    "In Stock";
            }
        }

        private bool SalesOrderExists(int id)
        {
            return _context.SalesOrders
                .Any(e => e.Id == id);
        }
    }
}
