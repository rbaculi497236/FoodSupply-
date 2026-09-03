using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Dashboard
        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();

            // =========================
            // BASIC COUNTS
            // =========================

            model.TotalProducts = await _context.Products.CountAsync();

            model.TotalCustomers = await _context.Customers.CountAsync();

            model.TotalSuppliers = await _context.Suppliers.CountAsync();

            model.TotalCategories = await _context.Categories.CountAsync();


            // =========================
            // INVENTORY
            // =========================

            // Low-stock calculation will be added
            // after checking the actual Inventory model.


            // =========================
            // PURCHASING
            // =========================

            model.PendingPurchases = await _context.Purchases
                .CountAsync(p =>
                    !p.IsArchived &&
                    p.Status == "Pending");


            // =========================
            // SALES ORDERS
            // =========================

            model.PendingSalesOrders = await _context.SalesOrders
                .CountAsync(s =>
                    !s.IsArchived &&
                    (s.Status == "Pending" ||
                     s.Status == "Processing"));


            model.TotalSales = await _context.SalesOrders
                .Where(s => !s.IsArchived)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;


            // =========================
            // DELIVERIES
            // =========================

            model.PendingDeliveries = await _context.Deliveries
                .CountAsync(d =>
                    !d.IsArchived &&
                    d.Status == "Pending");


            model.OutForDelivery = await _context.Deliveries
                .CountAsync(d =>
                    !d.IsArchived &&
                    d.Status == "Out for Delivery");


            model.CompletedDeliveries = await _context.Deliveries
                .CountAsync(d =>
                    !d.IsArchived &&
                    d.Status == "Delivered");


            // =========================
            // BILLING
            // =========================

            model.UnpaidInvoices = await _context.Billings
                .CountAsync(b =>
                    !b.IsArchived &&
                    b.PaymentStatus == "Unpaid");


            model.PartiallyPaidInvoices = await _context.Billings
                .CountAsync(b =>
                    !b.IsArchived &&
                    b.PaymentStatus == "Partially Paid");


            model.OutstandingBalance = await _context.Billings
                .Where(b => !b.IsArchived)
                .SumAsync(b => (decimal?)b.Balance) ?? 0;


            model.TotalPaid = await _context.Billings
                .Where(b => !b.IsArchived)
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0;


            // =========================
            // RECENT SALES ORDERS
            // =========================

            model.RecentSalesOrders = await _context.SalesOrders
                .Where(s => !s.IsArchived)
                .Include(s => s.Customer)
                .OrderByDescending(s => s.OrderDate)
                .Take(5)
                .ToListAsync();


            // =========================
            // RECENT BILLINGS
            // =========================

            model.RecentBillings = await _context.Billings
                .Where(b => !b.IsArchived)
                .Include(b => b.SalesOrder)
                .OrderByDescending(b => b.InvoiceDate)
                .Take(5)
                .ToListAsync();


            return View(model);
        }
    }
}