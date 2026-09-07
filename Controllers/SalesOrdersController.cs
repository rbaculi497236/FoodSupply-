using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    public class SalesOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: SalesOrders
        public async Task<IActionResult> Index()
        {
            var salesOrders = await _context.SalesOrders
                .Where(s => !s.IsArchived)
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(s => s.OrderDate)
                .ToListAsync();

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

        // GET: SalesOrders/Create
        public async Task<IActionResult> Create()
        {
            await LoadCustomers();
            await LoadProducts();

            return View();
        }

        // POST: SalesOrders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesOrder salesOrder)
        {
            var orderItems = salesOrder.SalesOrderItems
                ?? new List<SalesOrderItem>();

            ModelState.Remove("Customer");
            ModelState.Remove("SalesOrderItems");

            if (!orderItems.Any())
            {
                ModelState.AddModelError(
                    "",
                    "Please add at least one product to the sales order."
                );
            }

            if (!ModelState.IsValid)
            {
                await LoadCustomers(salesOrder.CustomerId);
                await LoadProducts();

                return View(salesOrder);
            }

            var productIds = orderItems
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            var products = await _context.Products
                .Where(p =>
                    productIds.Contains(p.Id) &&
                    p.Status == "Active")
                .ToDictionaryAsync(p => p.Id);

            decimal totalAmount = 0;

            // Validate products and inventory
            foreach (var item in orderItems)
            {
                if (!products.TryGetValue(
                        item.ProductId,
                        out var product))
                {
                    ModelState.AddModelError(
                        "",
                        $"Product ID {item.ProductId} was not found or is inactive."
                    );

                    continue;
                }

                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(
                        i => i.ProductId == item.ProductId);

                if (inventory == null)
                {
                    ModelState.AddModelError(
                        "",
                        $"No inventory record exists for {product.ProductName}."
                    );

                    continue;
                }

                if (item.Quantity > inventory.StockQuantity)
                {
                    ModelState.AddModelError(
                        "",
                        $"Insufficient stock for {product.ProductName}. " +
                        $"Available: {inventory.StockQuantity}, " +
                        $"Requested: {item.Quantity}."
                    );

                    continue;
                }

                item.UnitPrice = product.Price;

                item.Subtotal =
                    item.Quantity * item.UnitPrice;

                totalAmount += item.Subtotal;
            }

            if (!ModelState.IsValid)
            {
                await LoadCustomers(salesOrder.CustomerId);
                await LoadProducts();

                return View(salesOrder);
            }

            // New Sales Order always starts as Pending
            salesOrder.OrderDate = DateTime.Now;
            salesOrder.Status = "Pending";
            salesOrder.TotalAmount = totalAmount;
            salesOrder.IsArchived = false;

            _context.SalesOrders.Add(salesOrder);

            // Deduct inventory for new order
            foreach (var item in orderItems)
            {
                var inventory = await _context.Inventories
                    .FirstAsync(
                        i => i.ProductId == item.ProductId);

                var product =
                    products[item.ProductId];

                inventory.StockQuantity -= item.Quantity;
                inventory.LastUpdated = DateTime.Now;

                UpdateInventoryStatus(inventory);

                product.StockQuantity =
                    inventory.StockQuantity;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Sales Order #{salesOrder.Id} created successfully.";

            return RedirectToAction(nameof(Index));
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

        // POST: SalesOrders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            SalesOrder salesOrder)
        {
            if (id != salesOrder.Id)
            {
                return NotFound();
            }

            var newItems = salesOrder.SalesOrderItems
                ?? new List<SalesOrderItem>();

            ModelState.Remove("Customer");
            ModelState.Remove("SalesOrderItems");

            if (!newItems.Any() &&
                salesOrder.Status != "Cancelled")
            {
                ModelState.AddModelError(
                    "",
                    "Please add at least one product to the sales order."
                );
            }

            if (!ModelState.IsValid)
            {
                await LoadCustomers(salesOrder.CustomerId);
                await LoadProducts();

                return View(salesOrder);
            }

            // Get existing order
            var existingOrder = await _context.SalesOrders
                .Include(s => s.SalesOrderItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (existingOrder == null)
            {
                return NotFound();
            }

            // Prevent manually skipping the workflow
            //
            // Only Pending / Processing orders can be changed
            // through the Sales Order screen.
            //
            // Billed, Out for Delivery, and Delivered are controlled
            // by Billing and Delivery modules.

            if (existingOrder.Status == "Billed" ||
                existingOrder.Status == "Out for Delivery" ||
                existingOrder.Status == "Delivered")
            {
                ModelState.AddModelError(
                    "",
                    "This Sales Order has already progressed to Billing or Delivery and cannot be edited from the Sales Order module."
                );

                await LoadCustomers(existingOrder.CustomerId);
                await LoadProducts();

                return View(existingOrder);
            }

            // Only allow Pending, Processing, or Cancelled
            var allowedStatuses = new[]
            {
                "Pending",
                "Processing",
                "Cancelled"
            };

            if (!allowedStatuses.Contains(salesOrder.Status))
            {
                ModelState.AddModelError(
                    "Status",
                    "Invalid Sales Order status."
                );

                await LoadCustomers(existingOrder.CustomerId);
                await LoadProducts();

                return View(existingOrder);
            }

            // Get new product IDs
            var productIds = newItems
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            var products = await _context.Products
                .Where(p =>
                    productIds.Contains(p.Id) &&
                    p.Status == "Active")
                .ToDictionaryAsync(p => p.Id);

            // Get old product IDs
            var oldProductIds = existingOrder.SalesOrderItems
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            // Get all affected products
            var allProductIds = oldProductIds
                .Union(productIds)
                .Distinct()
                .ToList();

            var inventories = await _context.Inventories
                .Where(i =>
                    allProductIds.Contains(i.ProductId))
                .ToDictionaryAsync(i => i.ProductId);

            // Restore old inventory
            // Only restore if the old order was not cancelled.
            if (existingOrder.Status != "Cancelled")
            {
                foreach (var oldItem in existingOrder.SalesOrderItems)
                {
                    if (inventories.TryGetValue(
                            oldItem.ProductId,
                            out var inventory))
                    {
                        inventory.StockQuantity +=
                            oldItem.Quantity;

                        inventory.LastUpdated =
                            DateTime.Now;

                        UpdateInventoryStatus(
                            inventory);

                        var oldProduct =
                            await _context.Products
                                .FirstOrDefaultAsync(
                                    p => p.Id ==
                                         oldItem.ProductId);

                        if (oldProduct != null)
                        {
                            oldProduct.StockQuantity =
                                inventory.StockQuantity;
                        }
                    }
                }
            }

            decimal totalAmount = 0;

            // Validate and calculate new order
            if (salesOrder.Status != "Cancelled")
            {
                foreach (var item in newItems)
                {
                    if (!products.TryGetValue(
                            item.ProductId,
                            out var product))
                    {
                        ModelState.AddModelError(
                            "",
                            $"Product ID {item.ProductId} was not found or is inactive."
                        );

                        continue;
                    }

                    if (!inventories.TryGetValue(
                            item.ProductId,
                            out var inventory))
                    {
                        ModelState.AddModelError(
                            "",
                            $"No inventory record exists for {product.ProductName}."
                        );

                        continue;
                    }

                    if (item.Quantity >
                        inventory.StockQuantity)
                    {
                        ModelState.AddModelError(
                            "",
                            $"Insufficient stock for {product.ProductName}. " +
                            $"Available: {inventory.StockQuantity}, " +
                            $"Requested: {item.Quantity}."
                        );

                        continue;
                    }

                    item.UnitPrice =
                        product.Price;

                    item.Subtotal =
                        item.Quantity *
                        item.UnitPrice;

                    totalAmount +=
                        item.Subtotal;
                }

                if (!ModelState.IsValid)
                {
                    await LoadCustomers(
                        salesOrder.CustomerId);

                    await LoadProducts();

                    return View(salesOrder);
                }

                // Deduct new inventory
                foreach (var item in newItems)
                {
                    var inventory =
                        inventories[item.ProductId];

                    var product =
                        products[item.ProductId];

                    inventory.StockQuantity -=
                        item.Quantity;

                    inventory.LastUpdated =
                        DateTime.Now;

                    UpdateInventoryStatus(
                        inventory);

                    product.StockQuantity =
                        inventory.StockQuantity;
                }
            }

            // Update order
            existingOrder.CustomerId =
                salesOrder.CustomerId;

            existingOrder.Status =
                salesOrder.Status;

            existingOrder.Remarks =
                salesOrder.Remarks;

            existingOrder.TotalAmount =
                salesOrder.Status == "Cancelled"
                    ? 0
                    : totalAmount;

            // Replace old items
            _context.SalesOrderItems.RemoveRange(
                existingOrder.SalesOrderItems);

            foreach (var item in newItems)
            {
                item.Id = 0;

                item.SalesOrderId =
                    existingOrder.Id;

                if (salesOrder.Status == "Cancelled")
                {
                    item.UnitPrice = 0;
                    item.Subtotal = 0;
                }

                _context.SalesOrderItems.Add(item);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesOrderExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            TempData["SuccessMessage"] =
                $"Sales Order #{existingOrder.Id} updated successfully.";

            return RedirectToAction(nameof(Index));
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

            salesOrder.IsArchived = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Sales Order #{salesOrder.Id} archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: SalesOrders/Archived
        public async Task<IActionResult> Archived()
        {
            var archivedOrders = await _context.SalesOrders
                .Where(s => s.IsArchived)
                .Include(s => s.Customer)
                .Include(s => s.SalesOrderItems)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(s => s.OrderDate)
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
            var products = await _context.Products
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.ProductName)
                .ToListAsync();

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