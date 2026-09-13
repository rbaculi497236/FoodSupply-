using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
[Authorize(Roles = "Main Admin,Admin,Manager,Sales Staff / Billing Staff,Delivery Staff")]
public class ReportsController : Controller
{
private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================
    // REPORTS DASHBOARD
    // =========================
    public async Task<IActionResult> Index()
    {
        var model = new ReportsDashboardViewModel
        {
            TotalProducts = await _context.Products
                .CountAsync(p => !p.IsArchived),

            TotalCustomers = await _context.Customers
                .CountAsync(c => !c.IsArchived),

            TotalSuppliers = await _context.Suppliers
                .CountAsync(s => !s.IsArchived),

            TotalSales = await _context.SalesOrders
                .Where(s => !s.IsArchived)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0,

            TotalPurchases = await _context.Purchases
                .Where(p => !p.IsArchived)
                .SumAsync(p => (decimal?)p.TotalAmount) ?? 0,

            TotalBilled = await _context.Billings
                .Where(b => !b.IsArchived)
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0,

            TotalPaid = await _context.Billings
                .Where(b => !b.IsArchived)
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0,

            TotalBalance = await _context.Billings
                .Where(b => !b.IsArchived)
                .SumAsync(b => (decimal?)b.Balance) ?? 0,

            TotalOrders = await _context.SalesOrders
                .CountAsync(s => !s.IsArchived),

            TotalDeliveries = await _context.Deliveries
                .CountAsync(d => !d.IsArchived),

            LowStockProducts = await _context.Inventories
                .CountAsync(i =>
                    !i.IsArchived &&
                    i.StockQuantity <= i.ReorderLevel)
        };

        return View(model);
    }


    // =========================
    // SALES REPORT
    // =========================
    public async Task<IActionResult> Sales(
        DateTime? fromDate,
        DateTime? toDate)
    {
        var query = _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.SalesOrderItems)
            .Where(s => !s.IsArchived)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(s =>
                s.OrderDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var endDate = toDate.Value.Date.AddDays(1);

            query = query.Where(s =>
                s.OrderDate < endDate);
        }

        var orders = await query
            .OrderByDescending(s => s.OrderDate)
            .Select(s => new SalesReportViewModel
            {
                Id = s.Id,
                OrderDate = s.OrderDate,

                CustomerName = s.Customer != null
                    ? s.Customer.CustomerName
                    : "Unknown Customer",

                Status = s.Status,
                TotalAmount = s.TotalAmount,

                ItemCount = s.SalesOrderItems != null
                    ? s.SalesOrderItems.Count
                    : 0
            })
            .ToListAsync();

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        ViewBag.TotalSales = orders.Sum(x => x.TotalAmount);
        ViewBag.TotalOrders = orders.Count;

        return View(orders);
    }


    // =========================
    // INVENTORY REPORT
    // =========================
    public async Task<IActionResult> Inventory(
        string? status)
    {
        var query = _context.Inventories
            .Include(i => i.Product)
                .ThenInclude(p => p!.Category)
            .Where(i => !i.IsArchived && i.Product != null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "Low Stock")
            {
                query = query.Where(i =>
                    i.StockQuantity <= i.ReorderLevel);
            }
            else if (status == "In Stock")
            {
                query = query.Where(i =>
                    i.StockQuantity > i.ReorderLevel);
            }
        }

        var inventory = await query
            .OrderBy(i => i.Product != null
                ? i.Product.ProductName
                : "")
            .Select(i => new InventoryReportViewModel
            {
                ProductCode = i.Product != null
                    ? i.Product.ProductCode
                    : "N/A",

                ProductName = i.Product != null
                    ? i.Product.ProductName
                    : "Unknown Product",

                CategoryName = i.Product != null &&
                               i.Product.Category != null
                    ? i.Product.Category.CategoryName
                    : "Uncategorized",

                Unit = i.Product != null
                    ? i.Product.Unit
                    : "N/A",

                StockQuantity = i.StockQuantity,
                ReorderLevel = i.ReorderLevel,

                StockStatus = i.StockStatus,

                DamagedQuantity = i.DamagedQuantity,
                SpoiledQuantity = i.SpoiledQuantity,

                LastUpdated = i.LastUpdated
            })
            .ToListAsync();

        ViewBag.Status = status;

        return View(inventory);
    }


    // =========================
    // PURCHASE REPORT
    // =========================
    public async Task<IActionResult> Purchases(
        DateTime? fromDate,
        DateTime? toDate)
    {
        var query = _context.Purchases
            .Include(p => p.Supplier)
            .Where(p => !p.IsArchived)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(p =>
                p.PurchaseDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var endDate = toDate.Value.Date.AddDays(1);

            query = query.Where(p =>
                p.PurchaseDate < endDate);
        }

        var purchases = await query
            .OrderByDescending(p => p.PurchaseDate)
            .Select(p => new PurchaseReportViewModel
            {
                Id = p.Id,

                PurchaseOrderNumber = p.PurchaseOrderNumber,

                PurchaseDate = p.PurchaseDate,

                SupplierName = p.Supplier != null
                    ? p.Supplier.SupplierName
                    : "Unknown Supplier",

                Status = p.Status,

                TotalAmount = p.TotalAmount
            })
            .ToListAsync();

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        ViewBag.TotalPurchases =
            purchases.Sum(x => x.TotalAmount);

        ViewBag.TotalPurchaseOrders =
            purchases.Count;

        return View(purchases);
    }


    // =========================
    // BILLING REPORT
    // =========================
    public async Task<IActionResult> Billing(
        string? paymentStatus)
    {
        var query = _context.Billings
            .Include(b => b.SalesOrder)
                .ThenInclude(s => s!.Customer)
            .Where(b => !b.IsArchived)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            query = query.Where(b =>
                b.PaymentStatus == paymentStatus);
        }

        var billings = await query
            .OrderByDescending(b => b.InvoiceDate)
            .Select(b => new BillingReportViewModel
            {
                Id = b.Id,

                InvoiceNumber = b.InvoiceNumber,

                InvoiceDate = b.InvoiceDate,

                DueDate = b.DueDate,

                CustomerName =
                    b.SalesOrder != null &&
                    b.SalesOrder.Customer != null
                        ? b.SalesOrder.Customer.CustomerName
                        : "Unknown Customer",

                PaymentStatus = b.PaymentStatus,

                TotalAmount = b.TotalAmount,

                AmountPaid = b.AmountPaid,

                Balance = b.Balance,

                PaymentDate = b.PaymentDate,

                PaymentMethod = b.PaymentMethod
            })
            .ToListAsync();

        ViewBag.PaymentStatus = paymentStatus;

        ViewBag.TotalBilled =
            billings.Sum(x => x.TotalAmount);

        ViewBag.TotalPaid =
            billings.Sum(x => x.AmountPaid);

        ViewBag.TotalBalance =
            billings.Sum(x => x.Balance);

        return View(billings);
    }


    // =========================
    // DELIVERY REPORT
    // =========================
    public async Task<IActionResult> Deliveries(
        string? status)
    {
        var query = _context.Deliveries
            .Include(d => d.SalesOrder)
                .ThenInclude(s => s!.Customer)
            .Where(d => !d.IsArchived)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d =>
                d.Status == status);
        }

        var deliveries = await query
            .OrderByDescending(d => d.DeliveryDate)
            .Select(d => new DeliveryReportViewModel
            {
                Id = d.Id,

                DeliveryDate = d.DeliveryDate,

                CustomerName =
                    d.SalesOrder != null &&
                    d.SalesOrder.Customer != null
                        ? d.SalesOrder.Customer.CustomerName
                        : "Unknown Customer",

                Status = d.Status,

                DeliveryAddress = d.DeliveryAddress,

                Driver = d.Driver,

                Vehicle = d.Vehicle
            })
            .ToListAsync();

        ViewBag.Status = status;

        return View(deliveries);
    }
}


}
