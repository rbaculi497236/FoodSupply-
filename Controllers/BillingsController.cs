using FoodSupply.Services;
using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
    public class BillingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PaymentService _payments;

        public BillingsController(ApplicationDbContext context, PaymentService payments)
        {
            _context = context; _payments = payments;
        }

        // GET: Billings
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Billings
                .Where(b => true)
                .Include(b => b.SalesOrder)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(b => b.InvoiceNumber.Contains(search) || b.PaymentStatus.Contains(search));
            var totalItems = await query.CountAsync();
            var pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, pageCount);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            var billings = await query.OrderByDescending(b => b.InvoiceDate)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return View(billings);
        }

        // GET: Billings/Create
        [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
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
        [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
        public async Task<IActionResult> Create([Bind("SalesOrderId,DueDate,Remarks")] Billing billing)
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
                    b.SalesOrderId == billing.SalesOrderId);

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

            ViewBag.Payments = await _context.Payments.Where(p => p.BillingId == id).OrderByDescending(p => p.Id).ToListAsync();
            return View(billing);
        }

        // GET: Billings/Edit/5
        [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, decimal amount, string paymentMethod, string? reference, string requestId)
        {
            var billing = await _context.Billings.FindAsync(id);
            if (billing == null) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    await _payments.RecordAsync(id, amount, paymentMethod, reference ?? "", requestId);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (InvalidOperationException ex) when (ex.Source == "FoodSupply.Business") { ModelState.AddModelError("", ex.Message); }
            }
            return View(billing);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Main Admin,Manager")]
        public async Task<IActionResult> ReversePayment(int paymentId, string reason, string requestId)
        {
            await _payments.ReverseAsync(paymentId, reason, requestId);
            await _context.SaveChangesAsync();
            var payment = await _context.Payments.FindAsync(paymentId);
            return RedirectToAction(nameof(Details), new { id = payment!.BillingId });
        }
        // GET: Billings/Archive/5
        [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
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
        [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
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
        public async Task<IActionResult> Archived(int page = 1)
        {
            const int pageSize = 10;
            var query = _context.Billings
                .Where(b => b.IsArchived)
                .Include(b => b.SalesOrder);
            var totalItems = await query.CountAsync();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
            ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
            var billings = await query.OrderByDescending(b => b.InvoiceDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return View(billings);
        }

        // POST: Billings/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
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
                .Where(b => true)
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
