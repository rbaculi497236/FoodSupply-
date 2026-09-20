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
                .Where(s => s.Status == "Delivered")
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0,

            TotalPurchases = await _context.Purchases
                .Where(p => p.Status != "Cancelled")
                .SumAsync(p => (decimal?)p.TotalAmount) ?? 0,

            TotalBilled = await _context.Billings
                .Where(b => true)
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0,

            TotalPaid = await _context.Billings
                .Where(b => true)
                .SumAsync(b => (decimal?)b.AmountPaid) ?? 0,

            TotalBalance = await _context.Billings
                .Where(b => true)
                .SumAsync(b => (decimal?)b.Balance) ?? 0,

            TotalOrders = await _context.SalesOrders
                .CountAsync(),

            TotalDeliveries = await _context.Deliveries
                .CountAsync(),

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
        DateTime? toDate,
        int page = 1)
    {
        var query = _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.SalesOrderItems)
            .Where(s => true)
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

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
        ViewBag.TotalSales = await query.Where(s => s.Status == "Delivered").SumAsync(s => (decimal?)s.TotalAmount) ?? 0;
        ViewBag.TotalOrders = totalItems;
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
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        ViewBag.PaginationRouteValues = new Dictionary<string, string>
        {
            ["fromDate"] = fromDate?.ToString("yyyy-MM-dd") ?? "",
            ["toDate"] = toDate?.ToString("yyyy-MM-dd") ?? ""
        };

        return View(orders);
    }


    // =========================
    // INVENTORY REPORT
    // =========================
    public async Task<IActionResult> Inventory(
        string? status,
        int page = 1)
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

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
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
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.PaginationRouteValues = new Dictionary<string, string> { ["status"] = status ?? "" };

        return View(inventory);
    }


    // =========================
    // PURCHASE REPORT
    // =========================
    public async Task<IActionResult> Purchases(
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1)
    {
        var query = _context.Purchases
            .Include(p => p.Supplier)
            .Where(p => p.Status != "Cancelled")
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

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
        ViewBag.TotalPurchases = await query.SumAsync(p => (decimal?)p.TotalAmount) ?? 0;
        ViewBag.TotalPurchaseOrders = totalItems;
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
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        ViewBag.PaginationRouteValues = new Dictionary<string, string>
        {
            ["fromDate"] = fromDate?.ToString("yyyy-MM-dd") ?? "",
            ["toDate"] = toDate?.ToString("yyyy-MM-dd") ?? ""
        };

        return View(purchases);
    }


    // =========================
    // BILLING REPORT
    // =========================
    public async Task<IActionResult> Billing(
        string? paymentStatus,
        int page = 1)
    {
        var query = _context.Billings
            .Include(b => b.SalesOrder)
                .ThenInclude(s => s!.Customer)
            .Where(b => true)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            query = query.Where(b =>
                b.PaymentStatus == paymentStatus);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
        ViewBag.TotalBilled = await query.SumAsync(b => (decimal?)b.TotalAmount) ?? 0;
        ViewBag.TotalPaid = await query.SumAsync(b => (decimal?)b.AmountPaid) ?? 0;
        ViewBag.TotalBalance = await query.SumAsync(b => (decimal?)b.Balance) ?? 0;
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
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        ViewBag.PaymentStatus = paymentStatus;

        ViewBag.PaginationRouteValues = new Dictionary<string, string> { ["paymentStatus"] = paymentStatus ?? "" };

        return View(billings);
    }


    // =========================
    // DELIVERY REPORT
    // =========================
    public async Task<IActionResult> Deliveries(
        string? status,
        int page = 1)
    {
        var query = _context.Deliveries
            .Include(d => d.SalesOrder)
                .ThenInclude(s => s!.Customer)
            .Where(d => true)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d =>
                d.Status == status);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize)));
        ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalItems = totalItems;
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
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.PaginationRouteValues = new Dictionary<string, string> { ["status"] = status ?? "" };

        return View(deliveries);
    }
}


}
