using FoodSupply.Controllers;
using FoodSupply.Data;
using FoodSupply.Models;
using FoodSupply.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FoodSupply.Tests;

public sealed class WorkflowTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private ApplicationDbContext db = null!;
    private StockService stock = null!;
    private Product product = null!;
    private Customer customer = null!;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = NewContext();
        await db.Database.EnsureCreatedAsync();
        var supplier = new Supplier { SupplierCode = "S1", SupplierName = "Supplier" };
        var category = new Category { CategoryCode = "C1", CategoryName = "Food" };
        product = new Product { ProductCode = "P1", ProductName = "Rice", Supplier = supplier, Category = category, Unit = "Bag", Price = 10m };
        customer = new Customer { CustomerCode = "C1", CustomerName = "Customer" };
        db.AddRange(product, customer);
        await db.SaveChangesAsync();
        stock = new StockService(db);
    }
    private ApplicationDbContext NewContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private SalesOrder Input(params int[] quantities) => new() { CustomerId = customer.Id,
        SalesOrderItems = quantities.Select(q => new SalesOrderItem { ProductId = product.Id, Quantity = q }).ToList() };
    private async Task Receive(int quantity, string batch = "B1", int days = 30, bool quarantine = false)
    {
        await stock.ReceiveAsync(product.Id, quantity, batch, DateTime.Today.AddDays(days), quarantine, "Test opening stock");
        await db.SaveChangesAsync();
    }
    private async Task<SalesOrder> Order(params int[] quantities) => await new SalesOrderService(db, stock).SaveAsync(Input(quantities));

    [Fact]
    public async Task NotificationsIncludeBatchAlertsAndExcludeHealthyOrArchivedStock()
    {
        await Receive(10);
        var inventory = await db.Inventories.SingleAsync();
        inventory.ReorderLevel = 0;
        inventory.ExpirationDate = null;
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db);
        async Task<List<InventoryNotification>> Read(int page = 1)
        {
            var result = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.Index(page));
            return Assert.IsType<List<InventoryNotification>>(result.Model);
        }
        // Receive defaults to the inclusive 30-day expiry boundary.
        var notification = Assert.Single(await Read(-1));
        Assert.True(notification.Expiring);
        Assert.False(notification.LowStock);
        Assert.Equal("Rice", notification.ProductName);

        var batch = await db.InventoryBatches.SingleAsync();
        batch.ExpirationDate = DateTime.Today.AddDays(31);
        await db.SaveChangesAsync();
        Assert.Empty(await Read());
        batch.IsQuarantined = true;
        await db.SaveChangesAsync();
        Assert.True(Assert.Single(await Read(999)).Quarantined);
        batch.Quantity = 0;
        await db.SaveChangesAsync();
        Assert.Empty(await Read());

        inventory.StockQuantity = 0;
        inventory.SpoiledQuantity = 2;
        inventory.DamagedQuantity = 1;
        await db.SaveChangesAsync();
        notification = Assert.Single(await Read());
        Assert.True(notification.LowStock);
        Assert.Equal(2, notification.SpoiledQuantity);
        Assert.Equal(1, notification.DamagedQuantity);
        inventory.IsArchived = true;
        await db.SaveChangesAsync();
        Assert.Empty(await Read());
    }

    [Fact]
    public async Task InventoryAlertCorrectionsRequireReasonAndPreserveStock()
    {
        await Receive(10, days: 60);
        var inventory = await db.Inventories.SingleAsync();
        inventory.ReorderLevel = 0;
        inventory.ExpirationDate = DateTime.Today.AddDays(-1);
        inventory.SpoiledQuantity = 2;
        inventory.DamagedQuantity = 1;
        await db.SaveChangesAsync();
        var input = new Inventory { Id = inventory.Id, ProductId = product.Id,
            StockQuantity = inventory.StockQuantity, ReorderLevel = 0 };
        var rejected = new InventoriesController(db);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await rejected.Edit(inventory.Id, input, null));
        Assert.False(rejected.ModelState.IsValid);
        Assert.Equal(2, inventory.SpoiledQuantity);
        Assert.NotNull(inventory.ExpirationDate);

        var accepted = new InventoriesController(db);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(
            await accepted.Edit(inventory.Id, input, "Old records reconciled with batch history"));
        Assert.Equal(10, inventory.StockQuantity);
        Assert.Equal(10, (await db.InventoryBatches.SingleAsync()).Quantity);
        Assert.Equal("Old records reconciled with batch history", db.AuditReason);
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await new NotificationsController(db).Index());
        Assert.Empty(Assert.IsType<List<InventoryNotification>>(result.Model));
    }

    [Fact]
    public async Task ReceiptsUseSavedOrderValuesAndRejectMissingOrders()
    {
        await Receive(10);
        var order = await Order(3);
        customer.Address = "Customer address";
        var purchase = new Purchase
        {
            SupplierId = product.SupplierId, PurchaseOrderNumber = "PO-TEST", TotalAmount = 14m,
            PurchaseItems = [new PurchaseItem { ProductId = product.Id, Quantity = 2, UnitPrice = 7m, Subtotal = 14m }]
        };
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sales = new SalesOrdersController(db, new SalesOrderService(db, stock));
        var purchases = new PurchasesController(db);
        var salesView = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await sales.Receipt(order.Id));
        var salesReceipt = Assert.IsType<ReceiptViewModel>(salesView.Model);
        Assert.Equal("Customer address", salesReceipt.PartyAddress);
        Assert.Equal(30m, salesReceipt.Total);
        Assert.Equal(new ReceiptLine(3, "Rice", 10m, 30m), Assert.Single(salesReceipt.Items));
        Assert.False(salesReceipt.IsPurchase);

        var purchaseView = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await purchases.Receipt(purchase.Id));
        var purchaseReceipt = Assert.IsType<ReceiptViewModel>(purchaseView.Model);
        Assert.Equal("Supplier", purchaseReceipt.PartyName);
        Assert.Equal("PO-TEST", purchaseReceipt.Number);
        Assert.Equal(14m, purchaseReceipt.Total);
        Assert.Equal(new ReceiptLine(2, "Rice", 7m, 14m), Assert.Single(purchaseReceipt.Items));
        Assert.True(purchaseReceipt.IsPurchase);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await sales.Receipt(null));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await sales.Receipt(int.MaxValue));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await purchases.Receipt(null));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await purchases.Receipt(int.MaxValue));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task DuplicateLinesCannotOversell()
    {
        await Receive(10);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Order(6, 6));
        Assert.Equal(10, (await db.Inventories.SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task AllocationUsesEarliestUsableBatchAndSynchronizesProduct()
    {
        await Receive(4, "LATE", 40); await Receive(4, "EARLY", 10);
        await Receive(20, "EXPIRED", -1); await Receive(20, "QUARANTINE", 5, true);
        var order = await Order(3, 2);
        Assert.Single(order.SalesOrderItems);
        Assert.Equal(0, (await db.InventoryBatches.SingleAsync(b => b.BatchNumber == "EARLY")).Quantity);
        Assert.Equal(3, (await db.InventoryBatches.SingleAsync(b => b.BatchNumber == "LATE")).Quantity);
        Assert.Equal(43, product.StockQuantity);
        Assert.Equal(product.StockQuantity, (await db.Inventories.SingleAsync()).StockQuantity);
        Assert.Equal(50m, order.TotalAmount);
    }

    [Fact]
    public async Task ExpiredAndQuarantinedStockCannotBeSold()
    {
        await Receive(20, "OLD", -1); await Receive(20, "HOLD", 20, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Order(1));
    }

    [Fact]
    public async Task CancellingTwiceRestoresOriginalBatchesOnlyOnceAndKeepsHistory()
    {
        await Receive(10);
        var order = await Order(4);
        var service = new SalesOrderService(db, stock);
        await service.SaveAsync(new SalesOrder { Status = "Cancelled" }, order.Id);
        await service.SaveAsync(new SalesOrder { Status = "Cancelled" }, order.Id);
        Assert.Equal(10, (await db.Inventories.SingleAsync()).StockQuantity);
        Assert.Equal(40m, order.TotalAmount);
        Assert.Equal(4, order.SalesOrderItems.Single().Quantity);
    }

    [Fact]
    public async Task PaymentsAreIdempotentAndReversalsAreTraceable()
    {
        await Receive(10); var order = await Order(4);
        var billing = new Billing { SalesOrder = order, InvoiceNumber = "INV1", TotalAmount = 40m, Balance = 40m };
        db.Billings.Add(billing); await db.SaveChangesAsync();
        var service = new PaymentService(db);
        var payment = await service.RecordAsync(billing.Id, 15m, "Cash", "Receipt1", "request1"); await db.SaveChangesAsync();
        await service.RecordAsync(billing.Id, 15m, "Cash", "Receipt1", "request1"); await db.SaveChangesAsync();
        Assert.Equal(15m, billing.AmountPaid); Assert.Single(await db.Payments.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(billing.Id, 30m, "Cash", "", "request2"));
        await service.ReverseAsync(payment.Id, "Entry correction", "reverse1"); await db.SaveChangesAsync();
        await service.ReverseAsync(payment.Id, "Entry correction", "reverse1"); await db.SaveChangesAsync();
        Assert.Equal(0m, billing.AmountPaid); Assert.Equal(40m, billing.Balance);
        Assert.Equal(2, await db.Payments.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseAsync(payment.Id, "Again", "reverse2"));
    }

    [Fact]
    public async Task PartialReceiptsRejectExcessAndCountDamagedSeparately()
    {
        var purchase = new Purchase { SupplierId = product.SupplierId, PurchaseOrderNumber = "PO1",
            PurchaseItems = new List<PurchaseItem> { new() { ProductId = product.Id, Quantity = 10, UnitPrice = 5m, Subtotal = 50m } } };
        db.Purchases.Add(purchase); await db.SaveChangesAsync();
        var item = purchase.PurchaseItems.Single(); var operations = new OperationsService(db, stock);
        await operations.ReceiveAsync(item.Id, 4, 0, 1, "B1", DateTime.Today.AddDays(30), "One damaged", "receipt1");
        Assert.Equal("Partially Received", purchase.Status); Assert.Equal(4, product.StockQuantity);
        await operations.ReceiveAsync(item.Id, 4, 0, 1, "B1", DateTime.Today.AddDays(30), "One damaged", "receipt1");
        Assert.Single(await db.PurchaseReceipts.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.ReceiveAsync(item.Id, 6, 0, 0, "B2", null, "", "receipt2"));
        await operations.ReceiveAsync(item.Id, 5, 0, 0, "B2", null, "", "receipt3");
        Assert.Equal("Received", purchase.Status); Assert.Equal(9, product.StockQuantity);
    }

    [Fact]
    public async Task DeliveryStaffCanSelectDeliveredAndContinueToProofConfirmation()
    {
        await Receive(10);
        var order = await Order(3);
        var delivery = new Delivery { SalesOrder = order, Status = "Out for Delivery" };
        db.Deliveries.Add(delivery);
        await db.SaveChangesAsync();
        var controller = new DeliveriesController(db)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Delivery Staff")], "Test"))
                }
            }
        };
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(await controller.Edit(
            delivery.Id, new Delivery { Id = delivery.Id, SalesOrderId = order.Id, Status = "Delivered" }));
        Assert.Equal("Operations", result.ControllerName);
        Assert.Equal("Fulfill", result.ActionName);
        Assert.Equal(delivery.Id, result.RouteValues!["id"]);
        Assert.Equal("Out for Delivery", delivery.Status);
        var operations = new OperationsService(db, stock);
        await operations.DeliverAsync(delivery.Id, order.SalesOrderItems.Single().Id, 3, "Customer", "Signed note", "staff-completion");
        Assert.Equal("Delivered", delivery.Status);
        Assert.Equal("Delivered", order.Status);
    }

    [Fact]
    public async Task DeliveryProofPartialFulfillmentAndReturnsAreBounded()
    {
        await Receive(10); var order = await Order(5); order.Status = "Billed";
        var delivery = new Delivery { SalesOrder = order }; db.Deliveries.Add(delivery); await db.SaveChangesAsync();
        var operations = new OperationsService(db, stock); var item = order.SalesOrderItems.Single();
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.DeliverAsync(delivery.Id, item.Id, 2, "", "", "bad"));
        await operations.DeliverAsync(delivery.Id, item.Id, 2, "Recipient", "Signed note #1", "delivery1");
        Assert.Equal("Partially Delivered", order.Status);
        await operations.DeliverAsync(delivery.Id, item.Id, 3, "Recipient", "Signed note #2", "delivery2");
        Assert.Equal("Delivered", delivery.Status);
        await operations.ReturnAsync(item.Id, 1, "Damaged packaging", "return1");
        await operations.ReturnAsync(item.Id, 1, "Damaged packaging", "return1");
        Assert.Single(await db.CustomerReturns.ToListAsync());
        Assert.Equal(1, (await db.InventoryBatches.SingleAsync(b => b.IsQuarantined)).Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.ReturnAsync(item.Id, 5, "Too many", "return2"));
    }

    [Fact]
    public async Task ConcurrentStockEditsAreDetected()
    {
        await Receive(10);
        await using var other = NewContext();
        var first = await db.Inventories.SingleAsync(); var second = await other.Inventories.SingleAsync();
        first.ReorderLevel = 3; await db.SaveChangesAsync();
        second.ReorderLevel = 4;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public async Task ResetTokenIsSingleUseAndSecretsStayOutOfAudit()
    {
        var user = new User { Username = "alice", Email = "alice@example.com", FullName = "Alice", PasswordHash = "legacy" };
        db.Users.Add(user); await db.SaveChangesAsync();
        var email = new FakeEmail();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Application:PublicUrl"] = "https://food.example" }).Build();
        var service = new PasswordRecoveryService(db, email, configuration, NullLogger<PasswordRecoveryService>.Instance);
        await service.RequestAsync(user.Email);
        var token = email.Link!.Split("token=")[1]; var stamp = user.SecurityStamp;
        Assert.False(await service.ResetAsync(user.Id, new string('0', 64), "NewPassword123!"));
        Assert.True(await service.ResetAsync(user.Id, token, "NewPassword123!"));
        Assert.False(await service.ResetAsync(user.Id, token, "AnotherPassword!"));
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.DoesNotContain(await db.AuditEntries.ToListAsync(), a => a.Changes.Contains("PasswordHash") || a.Changes.Contains("ResetToken") || a.Changes.Contains(token));
    }

    [Fact]
    public async Task FailedOrderEditRollsBackReleasedStock()
    {
        await Receive(10); var order = await Order(4);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SalesOrderService(db, stock).SaveAsync(Input(20), order.Id));
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Equal(6, (await db.Inventories.SingleAsync()).StockQuantity);
        Assert.Equal(4, (await db.SalesOrderItems.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ArchivedTransactionsRemainInReportsAndPendingOrdersAreNotDeliveredSales()
    {
        await Receive(20); var completed = await Order(4); completed.Status = "Delivered";
        var pending = await Order(2);
        var invoice = new Billing { SalesOrder = completed, InvoiceNumber = "INV-HISTORY", TotalAmount = 40m, AmountPaid = 15m, Balance = 25m };
        db.Billings.Add(invoice); await db.SaveChangesAsync();
        var controller = new ReportsController(db);
        var before = (ReportModel(await controller.Index()));
        completed.IsArchived = true; invoice.IsArchived = true; await db.SaveChangesAsync();
        var after = ReportModel(await controller.Index());
        Assert.Equal(40m, after.TotalSales);
        Assert.Equal(before.TotalSales, after.TotalSales);
        Assert.Equal(before.TotalPaid, after.TotalPaid);
        Assert.Equal(before.TotalBalance, after.TotalBalance);
    }

    private static ReportsDashboardViewModel ReportModel(Microsoft.AspNetCore.Mvc.IActionResult result) =>
        Assert.IsType<ReportsDashboardViewModel>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(result).Model);

    [Fact]
    public async Task ExpiredPasswordTokenCannotChangePassword()
    {
        var user = new User { Username = "bob", Email = "bob@example.com", FullName = "Bob", PasswordHash = "old" };
        db.Users.Add(user); await db.SaveChangesAsync();
        var email = new FakeEmail();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Application:PublicUrl"] = "https://food.example" }).Build();
        var service = new PasswordRecoveryService(db, email, configuration, NullLogger<PasswordRecoveryService>.Instance);
        await service.RequestAsync(user.Email);
        var token = email.Link!.Split("token=")[1];
        user.ResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.False(await service.ResetAsync(user.Id, token, "ChangedPassword!"));
        Assert.Equal("old", user.PasswordHash);
    }

    [Fact]
    public async Task EditingOrderReplacesAllocationsWithoutLosingStock()
    {
        await Receive(10); var order = await Order(4);
        await new SalesOrderService(db, stock).SaveAsync(Input(7), order.Id);
        Assert.Equal(3, (await db.Inventories.SingleAsync()).StockQuantity);
        Assert.Equal(3, (await db.InventoryBatches.SingleAsync()).Quantity);
        Assert.Single(await db.SalesOrderItems.ToListAsync());
        Assert.Equal(70m, order.TotalAmount);
        Assert.Equal(-7, await db.StockMovements.Where(m => m.SalesOrderId == order.Id).SumAsync(m => m.Quantity));
    }

    private sealed class FakeEmail : IRecoveryEmailSender
    {
        public string? Link { get; private set; }
        public Task SendAsync(string email, string link) { Link = link; return Task.CompletedTask; }
    }

    private AccountController RegistrationController()
    {
        var recovery = new PasswordRecoveryService(db, new FakeEmail(),
            new ConfigurationBuilder().Build(), NullLogger<PasswordRecoveryService>.Instance);
        return new AccountController(db, recovery)
        {
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(), new TestTempDataProvider())
        };
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    public async Task PublicRegistrationCreatesSelectedRoleWithHashedPassword(string role)
    {
        var model = new AdminRegistrationViewModel { FullName = " New User ",
            Email = "new@example.com", Username = " newuser ", Role = role,
            Password = "LongPassword123!", ConfirmPassword = "LongPassword123!" };
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(
            await RegistrationController().RegisterAdmin(model));
        Assert.Equal("Login", result.ActionName);
        var user = await db.Users.SingleAsync();
        Assert.Equal(role, user.Role);
        Assert.Equal("newuser", user.Username);
        Assert.True(user.IsActive);
        Assert.False(user.IsArchived);
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, model.Password));

        var duplicate = RegistrationController();
        model.Username = "NEWUSER";
        model.Email = "NEW@EXAMPLE.COM";
        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await duplicate.RegisterAdmin(model));
        Assert.False(duplicate.ModelState.IsValid);
        Assert.Single(await db.Users.ToListAsync());
    }

    [Theory]
    [InlineData("Main Admin")]
    [InlineData("Delivery Staff")]
    public async Task PublicRegistrationRejectsOtherRoles(string role)
    {
        var controller = RegistrationController();
        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.RegisterAdmin(
            new AdminRegistrationViewModel { Role = role }));
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public void RegistrationValidatesPasswordLengthAndConfirmation()
    {
        var model = new AdminRegistrationViewModel { FullName = "Test", Email = "test@example.com",
            Username = "test", Password = "short", ConfirmPassword = "different" };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model,
            new System.ComponentModel.DataAnnotations.ValidationContext(model), results, true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.Password)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.ConfirmPassword)));
    }

    private sealed class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
    }
}
