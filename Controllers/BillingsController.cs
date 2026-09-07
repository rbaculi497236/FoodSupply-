using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    public class BillingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Billings
        public async Task<IActionResult> Index()
        {
            var billings = await _context.Billings
                .Where(b => !b.IsArchived)
                .Include(b => b.SalesOrder)
                .OrderByDescending(b => b.InvoiceDate)
                .ToListAsync();

            return View(billings);
        }

        // GET: Billings/Create
        public async Task<IActionResult> Create()
        {
            await LoadSalesOrders();

            return View(new Billing
            {
                InvoiceDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(30),
                PaymentStatus = "Unpaid",
                AmountPaid = 0
            });
        }

        // POST: Billings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Billing billing)
        {
            // System-generated fields
            ModelState.Remove("SalesOrder");
            ModelState.Remove("InvoiceNumber");
            ModelState.Remove("PaymentStatus");
            ModelState.Remove("TotalAmount");
            ModelState.Remove("AmountPaid");
            ModelState.Remove("Balance");
            ModelState.Remove("PaymentDate");
            ModelState.Remove("PaymentMethod");
            ModelState.Remove("Remarks");

            if (!ModelState.IsValid)
            {
                await LoadSalesOrders(billing.SalesOrderId);
                return View(billing);
            }

            // Get selected Sales Order
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == billing.SalesOrderId &&
                    !s.IsArchived &&
                    s.Status != "Cancelled");

            if (salesOrder == null)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "The selected Sales Order was not found, archived, or cancelled."
                );

                await LoadSalesOrders(billing.SalesOrderId);

                return View(billing);
            }

            // Prevent duplicate active invoice
            var existingBilling = await _context.Billings
                .AnyAsync(b =>
                    b.SalesOrderId == billing.SalesOrderId &&
                    !b.IsArchived);

            if (existingBilling)
            {
                ModelState.AddModelError(
                    "SalesOrderId",
                    "This Sales Order already has an active invoice."
                );

                await LoadSalesOrders(billing.SalesOrderId);

                return View(billing);
            }

            // Generate invoice number
            billing.InvoiceNumber =
                "INV-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");

            // Set invoice date
            billing.InvoiceDate = DateTime.Now;

            // Get total from Sales Order
            billing.TotalAmount =
                salesOrder.TotalAmount;

            // New invoice starts unpaid
            billing.AmountPaid = 0;

            billing.Balance =
                billing.TotalAmount;

            billing.PaymentStatus =
                "Unpaid";

            billing.PaymentDate = null;

            billing.PaymentMethod = null;

            billing.IsArchived = false;

            // Add Billing
            _context.Billings.Add(billing);

            // IMPORTANT:
            // Billing comes before Delivery.
            // Change Sales Order status to Billed.
            salesOrder.Status = "Billed";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Invoice {billing.InvoiceNumber} created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Billings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var billing = await _context.Billings
                .Include(b => b.SalesOrder)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (billing == null)
                return NotFound();

            return View(billing);
        }

        // GET: Billings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var billing = await _context.Billings
                .FirstOrDefaultAsync(b => b.Id == id);

            if (billing == null)
                return NotFound();

            return View(billing);
        }

        // POST: Billings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Billing billing)
        {
            if (id != billing.Id)
                return NotFound();

            ModelState.Remove("SalesOrder");
            ModelState.Remove("InvoiceNumber");
            ModelState.Remove("PaymentStatus");
            ModelState.Remove("TotalAmount");
            ModelState.Remove("Balance");

            if (!ModelState.IsValid)
                return View(billing);

            var existingBilling = await _context.Billings
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBilling == null)
                return NotFound();

            // Validate payment
            if (billing.AmountPaid < 0)
            {
                ModelState.AddModelError(
                    "AmountPaid",
                    "Amount paid cannot be negative."
                );

                return View(billing);
            }

            if (billing.AmountPaid >
                existingBilling.TotalAmount)
            {
                ModelState.AddModelError(
                    "AmountPaid",
                    "Amount paid cannot exceed the total amount."
                );

                return View(billing);
            }

            // Update amount paid
            existingBilling.AmountPaid =
                billing.AmountPaid;

            // Update balance
            existingBilling.Balance =
                existingBilling.TotalAmount -
                existingBilling.AmountPaid;

            // Update payment status
            if (existingBilling.AmountPaid == 0)
            {
                existingBilling.PaymentStatus =
                    "Unpaid";

                existingBilling.PaymentDate =
                    null;
            }
            else if (
                existingBilling.AmountPaid <
                existingBilling.TotalAmount)
            {
                existingBilling.PaymentStatus =
                    "Partially Paid";

                existingBilling.PaymentDate =
                    billing.PaymentDate;
            }
            else
            {
                existingBilling.PaymentStatus =
                    "Paid";

                existingBilling.PaymentDate =
                    billing.PaymentDate ??
                    DateTime.Now;
            }

            // Update other payment information
            existingBilling.DueDate =
                billing.DueDate;

            existingBilling.PaymentMethod =
                billing.PaymentMethod;

            existingBilling.Remarks =
                billing.Remarks;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Invoice {existingBilling.InvoiceNumber} updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Billings/Archive/5
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
                return NotFound();

            var billing = await _context.Billings
                .Include(b => b.SalesOrder)
                .FirstOrDefaultAsync(b =>
                    b.Id == id &&
                    !b.IsArchived);

            if (billing == null)
                return NotFound();

            return View(billing);
        }

        // POST: Billings/ArchiveConfirmed/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var billing = await _context.Billings
                .FirstOrDefaultAsync(b =>
                    b.Id == id &&
                    !b.IsArchived);

            if (billing == null)
                return NotFound();

            billing.IsArchived = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Invoice {billing.InvoiceNumber} archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Billings/Archived
        public async Task<IActionResult> Archived()
        {
            var billings = await _context.Billings
                .Where(b => b.IsArchived)
                .Include(b => b.SalesOrder)
                .OrderByDescending(b => b.InvoiceDate)
                .ToListAsync();

            return View(billings);
        }

        // POST: Billings/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var billing = await _context.Billings
                .FirstOrDefaultAsync(b =>
                    b.Id == id &&
                    b.IsArchived);

            if (billing == null)
                return NotFound();

            // Make sure Sales Order still exists
            var salesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == billing.SalesOrderId &&
                    !s.IsArchived &&
                    s.Status != "Cancelled");

            if (salesOrder == null)
            {
                TempData["ErrorMessage"] =
                    "The invoice cannot be restored because its Sales Order is unavailable.";

                return RedirectToAction(nameof(Archived));
            }

            // Check if another active invoice already exists
            var duplicateInvoice = await _context.Billings
                .AnyAsync(b =>
                    b.SalesOrderId == billing.SalesOrderId &&
                    !b.IsArchived &&
                    b.Id != billing.Id);

            if (duplicateInvoice)
            {
                TempData["ErrorMessage"] =
                    "The invoice cannot be restored because this Sales Order already has another active invoice.";

                return RedirectToAction(nameof(Archived));
            }

            billing.IsArchived = false;

            // Restore Sales Order to Billed
            salesOrder.Status = "Billed";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Invoice {billing.InvoiceNumber} restored successfully.";

            return RedirectToAction(nameof(Archived));
        }

        // Load Sales Orders that can be billed
        private async Task LoadSalesOrders(
            int? selectedSalesOrderId = null)
        {
            // Find Sales Orders that already have
            // an active invoice.
            var billedOrderIds = await _context.Billings
                .Where(b => !b.IsArchived)
                .Select(b => b.SalesOrderId)
                .Distinct()
                .ToListAsync();

            // Only show Sales Orders that:
            // 1. Are not archived
            // 2. Are not cancelled
            // 3. Do not already have an active invoice
            var orders = await _context.SalesOrders
                .Where(s =>
                    !s.IsArchived &&
                    s.Status != "Cancelled" &&
                    !billedOrderIds.Contains(s.Id))
                .OrderByDescending(s => s.OrderDate)
                .ToListAsync();

            ViewBag.SalesOrders =
                orders;

            ViewBag.SelectedSalesOrderId =
                selectedSalesOrderId;
        }
    }
}