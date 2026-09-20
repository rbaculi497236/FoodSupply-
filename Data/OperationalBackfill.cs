using Microsoft.EntityFrameworkCore.Migrations;

namespace FoodSupply.Data;

public static class OperationalBackfill
{
    // Freeze this SQL: it is part of the OperationalIntegrity migration.
    public static void Apply(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO Inventories (ProductId, StockQuantity, ReorderLevel, StockStatus, LastUpdated, ExpirationDate, SpoiledQuantity, DamagedQuantity, IsArchived, Version)
            SELECT p.Id, p.StockQuantity, p.ReorderLevel,
                CASE WHEN p.StockQuantity = 0 THEN 'Out of Stock' WHEN p.StockQuantity <= p.ReorderLevel THEN 'Low Stock' ELSE 'In Stock' END,
                UTC_TIMESTAMP(), p.ExpirationDate, 0, 0, p.IsArchived, UUID()
            FROM Products p WHERE NOT EXISTS (SELECT 1 FROM Inventories i WHERE i.ProductId = p.Id);
            """);
        migrationBuilder.Sql("""
            UPDATE Products p INNER JOIN Inventories i ON i.ProductId = p.Id SET p.StockQuantity = i.StockQuantity;
            """);
        migrationBuilder.Sql("""
            INSERT INTO InventoryBatches (ProductId, BatchNumber, ReceivedAt, ExpirationDate, Quantity, IsQuarantined, Version)
            SELECT ProductId, CONCAT('LEGACY-', Id), UTC_TIMESTAMP(), ExpirationDate, StockQuantity,
                CASE WHEN SpoiledQuantity > 0 OR DamagedQuantity > 0 THEN 1 ELSE 0 END, UUID() FROM Inventories;
            """);
        migrationBuilder.Sql("""
            INSERT INTO StockMovements (InventoryBatchId, SalesOrderId, Quantity, CreatedAt, Actor, Reason)
            SELECT b.Id, NULL, b.Quantity, UTC_TIMESTAMP(), 'migration', 'Opening stock snapshot'
            FROM InventoryBatches b WHERE b.BatchNumber LIKE 'LEGACY-%';
            """);
        // These paired entries reconstruct active reservations without changing on-hand stock.
        migrationBuilder.Sql("""
            INSERT INTO StockMovements (InventoryBatchId, SalesOrderId, Quantity, CreatedAt, Actor, Reason)
            SELECT b.Id, NULL, si.Quantity, UTC_TIMESTAMP(), 'migration', 'Opening reservation snapshot'
            FROM SalesOrderItems si INNER JOIN SalesOrders s ON s.Id = si.SalesOrderId
            INNER JOIN InventoryBatches b ON b.ProductId = si.ProductId AND b.BatchNumber LIKE 'LEGACY-%'
            WHERE s.Status IN ('Pending', 'Processing');
            """);
        migrationBuilder.Sql("""
            INSERT INTO StockMovements (InventoryBatchId, SalesOrderId, Quantity, CreatedAt, Actor, Reason)
            SELECT b.Id, s.Id, -si.Quantity, UTC_TIMESTAMP(), 'migration', 'Sales allocation'
            FROM SalesOrderItems si INNER JOIN SalesOrders s ON s.Id = si.SalesOrderId
            INNER JOIN InventoryBatches b ON b.ProductId = si.ProductId AND b.BatchNumber LIKE 'LEGACY-%'
            WHERE s.Status IN ('Pending', 'Processing');
            """);
        migrationBuilder.Sql("""
            INSERT INTO Payments (BillingId, Amount, PaidAt, Method, Reference, RequestId, ReversesPaymentId, Actor, Reason)
            SELECT Id, AmountPaid, COALESCE(PaymentDate, InvoiceDate), COALESCE(PaymentMethod, 'Legacy'),
                'Opening balance; individual historical receipts unavailable', CONCAT('legacy-billing-', Id), NULL, 'migration', 'Opening paid balance'
            FROM Billings WHERE AmountPaid > 0;
            """);
        migrationBuilder.Sql("""
            INSERT INTO PurchaseReceipts (PurchaseItemId, AcceptedQuantity, RejectedQuantity, DamagedQuantity, ReceivedAt, Actor, Reason, RequestId)
            SELECT i.Id, i.Quantity, 0, 0, p.PurchaseDate, 'migration', 'Legacy received purchase; original receipt details unavailable', CONCAT('legacy-purchase-item-', i.Id)
            FROM PurchaseItems i INNER JOIN Purchases p ON p.Id = i.PurchaseId WHERE p.Status = 'Received';
            """);
        migrationBuilder.Sql("""
            INSERT INTO DeliveryReceipts (DeliveryId, SalesOrderItemId, Quantity, DeliveredAt, ReceivedBy, ProofReference, Actor, RequestId)
            SELECT d.Id, i.Id, i.Quantity, d.DeliveryDate, 'Legacy recipient unavailable', 'Migrated completed delivery; no original proof captured', 'migration', CONCAT('legacy-delivery-item-', i.Id)
            FROM SalesOrderItems i INNER JOIN Deliveries d ON d.SalesOrderId = i.SalesOrderId
            WHERE d.Status = 'Delivered' AND d.Id = (SELECT MIN(d2.Id) FROM Deliveries d2 WHERE d2.SalesOrderId = d.SalesOrderId AND d2.Status = 'Delivered');
            """);
        migrationBuilder.Sql("""
            UPDATE Users SET SecurityStamp = UUID();
            """);
    }
}
