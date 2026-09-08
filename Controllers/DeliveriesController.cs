using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Main Admin,Delivery Staff")]
    public class DeliveriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DeliveriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Deliveries
        public async Task<IActionResult> Index()
        {
            var deliveries = await _context.Deliveries
                .Where(d => !d.IsArchived)
                .Include(d => d.SalesOrder)
                .OrderByDescending(d => d.DeliveryDate)
                .ToListAsync();

            return View(deliveries);
        }

        // GET: Deliveries/Create
        public async Task<IActionResult> Create()
        {
            await LoadSalesOrders();

            return View();
        }

        // POST: Deliveries/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Delivery delivery)
        {
            ModelState.Remove("SalesOrder");

            if (!ModelState.IsValid)
            {
                await LoadSalesOrders(delivery.SalesOrderId);
                return View(delivery);
            }

            // Get Sales Order
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == delivery.SalesOrderId &&
                    !s.IsArchived &&
                    s.Status != "Cancelled");

            if (salesOrder == null)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "The selected Sales Order was not found, archived, or cancelled."
                );

                await LoadSalesOrders(delivery.SalesOrderId);

                return View(delivery);
            }

            // Check if Sales Order has an active Billing
            var billing = await _context.Billings
                .FirstOrDefaultAsync(b =>
                    b.SalesOrderId == delivery.SalesOrderId &&
                    !b.IsArchived);

            if (billing == null)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "This Sales Order must be billed before a delivery can be created."
                );

                await LoadSalesOrders(delivery.SalesOrderId);

                return View(delivery);
            }

            // Make sure Sales Order is actually Billed
            if (salesOrder.Status != "Billed")
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "This Sales Order is not ready for delivery."
                );

                await LoadSalesOrders(delivery.SalesOrderId);

                return View(delivery);
            }

            // Prevent duplicate active delivery
            var existingDelivery = await _context.Deliveries
                .AnyAsync(d =>
                    d.SalesOrderId == delivery.SalesOrderId &&
                    !d.IsArchived);

            if (existingDelivery)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "This Sales Order already has an active delivery."
                );

                await LoadSalesOrders(delivery.SalesOrderId);

                return View(delivery);
            }

            delivery.DeliveryDate = DateTime.Now;
            delivery.IsArchived = false;

            _context.Deliveries.Add(delivery);

            // Update Sales Order status
            UpdateSalesOrderStatus(
                salesOrder,
                delivery.Status);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Delivery for Sales Order #{salesOrder.Id} created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Deliveries/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var delivery = await _context.Deliveries
                .Include(d => d.SalesOrder)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (delivery == null)
                return NotFound();

            return View(delivery);
        }

        // GET: Deliveries/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var delivery = await _context.Deliveries
                .FirstOrDefaultAsync(d => d.Id == id);

            if (delivery == null)
                return NotFound();

            await LoadSalesOrders(delivery.SalesOrderId);

            return View(delivery);
        }

        // POST: Deliveries/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Delivery delivery)
        {
            if (id != delivery.Id)
                return NotFound();

            ModelState.Remove("SalesOrder");

            if (!ModelState.IsValid)
            {
                await LoadSalesOrders(delivery.SalesOrderId);
                return View(delivery);
            }

            // Get existing delivery
            var existingDelivery = await _context.Deliveries
                .FirstOrDefaultAsync(d => d.Id == id);

            if (existingDelivery == null)
                return NotFound();

            // Get Sales Order
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == delivery.SalesOrderId &&
                    !s.IsArchived &&
                    s.Status != "Cancelled");

            if (salesOrder == null)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "The selected Sales Order was not found, archived, or cancelled."
                );

                await LoadSalesOrders(delivery.SalesOrderId);

                return View(delivery);
            }

            // Check Billing
            var billing = await _context.Billings
                .FirstOrDefaultAsync(b =>
                    b.SalesOrderId == delivery.SalesOrderId &&
                    !b.IsArchived);

            if (billing == null)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "This Sales Order must be billed before a delivery can be assigned."
                );

                await LoadSalesOrders(delivery.SalesOrderId);

                return View(delivery);
            }

            // If changing Sales Order
            if (existingDelivery.SalesOrderId != delivery.SalesOrderId)
            {
                // Check if new Sales Order already has a delivery
                var anotherDelivery = await _context.Deliveries
                    .AnyAsync(d =>
                        d.Id != existingDelivery.Id &&
                        d.SalesOrderId == delivery.SalesOrderId &&
                        !d.IsArchived);

                if (anotherDelivery)
                {
                    ModelState.AddModelError(
                        "SalesOrderId",
                        "The selected Sales Order already has an active delivery."
                    );

                    await LoadSalesOrders(delivery.SalesOrderId);

                    return View(delivery);
                }

                // Restore old Sales Order to Billed
                var oldSalesOrder = await _context.SalesOrders
                    .FirstOrDefaultAsync(s =>
                        s.Id == existingDelivery.SalesOrderId &&
                        !s.IsArchived);

                if (oldSalesOrder != null)
                {
                    var oldBilling = await _context.Billings
                        .AnyAsync(b =>
                            b.SalesOrderId == oldSalesOrder.Id &&
                            !b.IsArchived);

                    if (oldBilling)
                    {
                        oldSalesOrder.Status = "Billed";
                    }
                }
            }

            // Update delivery
            existingDelivery.SalesOrderId =
                delivery.SalesOrderId;

            existingDelivery.Status =
                delivery.Status;

            existingDelivery.DeliveryAddress =
                delivery.DeliveryAddress;

            existingDelivery.Driver =
                delivery.Driver;

            existingDelivery.Vehicle =
                delivery.Vehicle;

            existingDelivery.Remarks =
                delivery.Remarks;

            existingDelivery.DeliveryDate =
                delivery.DeliveryDate;

            // Update Sales Order status
            UpdateSalesOrderStatus(
                salesOrder,
                delivery.Status);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Delivery #{existingDelivery.Id} updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Deliveries/Archive/5
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
                return NotFound();

            var delivery = await _context.Deliveries
                .Include(d => d.SalesOrder)
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    !d.IsArchived);

            if (delivery == null)
                return NotFound();

            return View(delivery);
        }

        // POST: Deliveries/ArchiveConfirmed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var delivery = await _context.Deliveries
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    !d.IsArchived);

            if (delivery == null)
                return NotFound();

            delivery.IsArchived = true;

            // If the delivery was not completed,
            // return Sales Order to Billed.
            if (delivery.Status != "Delivered")
            {
                var salesOrder = await _context.SalesOrders
                    .FirstOrDefaultAsync(s =>
                        s.Id == delivery.SalesOrderId &&
                        !s.IsArchived);

                if (salesOrder != null)
                {
                    var billingExists = await _context.Billings
                        .AnyAsync(b =>
                            b.SalesOrderId == salesOrder.Id &&
                            !b.IsArchived);

                    if (billingExists)
                    {
                        salesOrder.Status = "Billed";
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Delivery #{delivery.Id} archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Deliveries/Archived
        public async Task<IActionResult> Archived()
        {
            var deliveries = await _context.Deliveries
                .Where(d => d.IsArchived)
                .Include(d => d.SalesOrder)
                .OrderByDescending(d => d.DeliveryDate)
                .ToListAsync();

            return View(deliveries);
        }

        // POST: Deliveries/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var delivery = await _context.Deliveries
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    d.IsArchived);

            if (delivery == null)
                return NotFound();

            // Check Sales Order
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == delivery.SalesOrderId &&
                    !s.IsArchived &&
                    s.Status != "Cancelled");

            if (salesOrder == null)
            {
                TempData["ErrorMessage"] =
                    "The delivery cannot be restored because its Sales Order is unavailable.";

                return RedirectToAction(nameof(Archived));
            }

            // Check Billing
            var billing = await _context.Billings
                .FirstOrDefaultAsync(b =>
                    b.SalesOrderId == delivery.SalesOrderId &&
                    !b.IsArchived);

            if (billing == null)
            {
                TempData["ErrorMessage"] =
                    "The delivery cannot be restored because its Sales Order is no longer billed.";

                return RedirectToAction(nameof(Archived));
            }

            // Prevent duplicate active delivery
            var duplicateDelivery = await _context.Deliveries
                .AnyAsync(d =>
                    d.Id != delivery.Id &&
                    d.SalesOrderId == delivery.SalesOrderId &&
                    !d.IsArchived);

            if (duplicateDelivery)
            {
                TempData["ErrorMessage"] =
                    "The delivery cannot be restored because this Sales Order already has another active delivery.";

                return RedirectToAction(nameof(Archived));
            }

            delivery.IsArchived = false;

            // Restore Sales Order status
            UpdateSalesOrderStatus(
                salesOrder,
                delivery.Status);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Delivery #{delivery.Id} restored successfully.";

            return RedirectToAction(nameof(Archived));
        }

        // Update Sales Order based on Delivery status
        private void UpdateSalesOrderStatus(
            SalesOrder salesOrder,
            string deliveryStatus)
        {
            if (deliveryStatus == "Delivered")
            {
                salesOrder.Status = "Delivered";
            }
            else if (deliveryStatus == "Out for Delivery")
            {
                salesOrder.Status = "Out for Delivery";
            }
            else if (deliveryStatus == "Pending")
            {
                // Do NOT move the Sales Order backward to Processing.
                // Billing already changed it to Billed.
                salesOrder.Status = "Billed";
            }
        }

        // Load only Sales Orders that have an active Billing
        private async Task LoadSalesOrders(
            int? selectedSalesOrderId = null)
        {
            var billedSalesOrderIds = await _context.Billings
                .Where(b => !b.IsArchived)
                .Select(b => b.SalesOrderId)
                .Distinct()
                .ToListAsync();

            var orders = await _context.SalesOrders
                .Where(s =>
                    !s.IsArchived &&
                    s.Status == "Billed" &&
                    billedSalesOrderIds.Contains(s.Id))
                .OrderByDescending(s => s.OrderDate)
                .ToListAsync();

            // When editing an existing delivery,
            // make sure its current Sales Order remains selectable.
            if (selectedSalesOrderId.HasValue &&
                !orders.Any(o => o.Id == selectedSalesOrderId.Value))
            {
                var selectedOrder = await _context.SalesOrders
                    .FirstOrDefaultAsync(s =>
                        s.Id == selectedSalesOrderId.Value &&
                        !s.IsArchived);

                if (selectedOrder != null)
                {
                    orders.Add(selectedOrder);
                }
            }

            ViewBag.SalesOrders = orders;

            ViewBag.SelectedSalesOrderId =
                selectedSalesOrderId;
        }
    }
}