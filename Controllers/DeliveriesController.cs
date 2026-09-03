using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
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

        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(s =>
                s.Id == delivery.SalesOrderId &&
                !s.IsArchived &&
                s.Status != "Cancelled");

        if (salesOrder == null)
        {
            ModelState.AddModelError(
                "SalesOrderId",
                "The selected sales order was not found or is cancelled."
            );

            await LoadSalesOrders(delivery.SalesOrderId);
            return View(delivery);
        }

        delivery.DeliveryDate = DateTime.Now;
        delivery.IsArchived = false;

        _context.Deliveries.Add(delivery);

        // Update Sales Order status based on Delivery status
        UpdateSalesOrderStatus(salesOrder, delivery.Status);

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

        var existingDelivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.Id == id);

        if (existingDelivery == null)
            return NotFound();

        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(s =>
                s.Id == delivery.SalesOrderId &&
                !s.IsArchived &&
                s.Status != "Cancelled");

        if (salesOrder == null)
        {
            ModelState.AddModelError(
                "SalesOrderId",
                "The selected sales order was not found or is cancelled."
            );

            await LoadSalesOrders(delivery.SalesOrderId);
            return View(delivery);
        }

        // If the Sales Order was changed, restore the old order status
        if (existingDelivery.SalesOrderId != delivery.SalesOrderId)
        {
            var oldSalesOrder = await _context.SalesOrders
                .FirstOrDefaultAsync(s =>
                    s.Id == existingDelivery.SalesOrderId);

            if (oldSalesOrder != null &&
                oldSalesOrder.Status == "Delivered")
            {
                oldSalesOrder.Status = "Processing";
            }
        }

        existingDelivery.SalesOrderId = delivery.SalesOrderId;
        existingDelivery.Status = delivery.Status;
        existingDelivery.DeliveryAddress = delivery.DeliveryAddress;
        existingDelivery.Driver = delivery.Driver;
        existingDelivery.Vehicle = delivery.Vehicle;
        existingDelivery.Remarks = delivery.Remarks;
        existingDelivery.DeliveryDate = delivery.DeliveryDate;

        // Update Sales Order status based on Delivery status
        UpdateSalesOrderStatus(salesOrder, delivery.Status);

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

        delivery.IsArchived = false;

        // Restore Sales Order status based on restored Delivery
        var salesOrder = await _context.SalesOrders
            .FirstOrDefaultAsync(s =>
                s.Id == delivery.SalesOrderId &&
                !s.IsArchived);

        if (salesOrder != null)
        {
            UpdateSalesOrderStatus(
                salesOrder,
                delivery.Status);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"Delivery #{delivery.Id} restored successfully.";

        return RedirectToAction(nameof(Archived));
    }

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
            salesOrder.Status = "Processing";
        }
    }

    private async Task LoadSalesOrders(
        int? selectedSalesOrderId = null)
    {
        var orders = await _context.SalesOrders
            .Where(s =>
                !s.IsArchived &&
                s.Status != "Cancelled")
            .OrderByDescending(s => s.OrderDate)
            .ToListAsync();

        ViewBag.SalesOrders = orders;

        ViewBag.SelectedSalesOrderId =
            selectedSalesOrderId;
    }
}

}
