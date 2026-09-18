using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Data;

/// <summary>
/// Adds a small, repeatable set of sample catalogue and inventory records.
/// It is safe to run more than once: records are identified by their codes.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedSampleProductsAsync(ApplicationDbContext context)
    {
        var categories = new[]
        {
            new Category { CategoryCode = "CAT-DRY", CategoryName = "Dry Goods", Description = "Shelf-stable pantry essentials" },
            new Category { CategoryCode = "CAT-CAN", CategoryName = "Canned Goods", Description = "Canned food and cooking ingredients" },
            new Category { CategoryCode = "CAT-BEV", CategoryName = "Beverages", Description = "Drinks and beverage supplies" },
            new Category { CategoryCode = "CAT-SNK", CategoryName = "Snacks", Description = "Ready-to-eat snack products" }
        };

        var suppliers = new[]
        {
            new Supplier { SupplierCode = "SUP-001", SupplierName = "Harvest Valley Foods", ContactPerson = "Maria Santos", PhoneNumber = "0917-555-0101", Email = "orders@harvestvalley.example", Address = "Quezon City" },
            new Supplier { SupplierCode = "SUP-002", SupplierName = "Pacific Pantry Supply", ContactPerson = "Daniel Cruz", PhoneNumber = "0917-555-0102", Email = "sales@pacificpantry.example", Address = "Manila" },
            new Supplier { SupplierCode = "SUP-003", SupplierName = "Fresh Choice Distributors", ContactPerson = "Angela Reyes", PhoneNumber = "0917-555-0103", Email = "hello@freshchoice.example", Address = "Makati" },
            new Supplier { SupplierCode = "SUP-004", SupplierName = "Golden Grain Trading", ContactPerson = "Ramon Lim", PhoneNumber = "0917-555-0104", Email = "orders@goldengrain.example", Address = "Pasig" }
        };

        var existingCategoryCodes = await context.Categories
            .Select(category => category.CategoryCode)
            .ToListAsync();
        context.Categories.AddRange(categories.Where(category => !existingCategoryCodes.Contains(category.CategoryCode)));

        var existingSupplierCodes = await context.Suppliers
            .Select(supplier => supplier.SupplierCode)
            .ToListAsync();
        context.Suppliers.AddRange(suppliers.Where(supplier => !existingSupplierCodes.Contains(supplier.SupplierCode)));
        await context.SaveChangesAsync();

        var categoryIds = await context.Categories.ToDictionaryAsync(category => category.CategoryCode, category => category.Id);
        var supplierIds = await context.Suppliers.ToDictionaryAsync(supplier => supplier.SupplierCode, supplier => supplier.Id);
        var expiration = DateTime.Today.AddMonths(12);

        var products = new[]
        {
            Product("PRD-1001", "Premium Jasmine Rice 5kg", "CAT-DRY", "SUP-004", "Bag", 40, 1, 325m, 40, 10),
            Product("PRD-1002", "All-Purpose Flour 1kg", "CAT-DRY", "SUP-004", "Pack", 60, 1, 58m, 60, 15),
            Product("PRD-1003", "Refined White Sugar 1kg", "CAT-DRY", "SUP-001", "Pack", 55, 1, 72m, 55, 15),
            Product("PRD-1004", "Iodized Salt 500g", "CAT-DRY", "SUP-001", "Pack", 80, 1, 22m, 80, 20),
            Product("PRD-1005", "Spaghetti Pasta 500g", "CAT-DRY", "SUP-002", "Pack", 48, 1, 46m, 48, 12),
            Product("PRD-1006", "Cooking Oil 1L", "CAT-DRY", "SUP-003", "Bottle", 36, 1, 118m, 36, 10),
            Product("PRD-1007", "Soy Sauce 1L", "CAT-DRY", "SUP-002", "Bottle", 42, 1, 68m, 42, 10),
            Product("PRD-1008", "Fish Sauce 750ml", "CAT-DRY", "SUP-002", "Bottle", 30, 1, 54m, 30, 8),
            Product("PRD-1009", "Canned Sardines 155g", "CAT-CAN", "SUP-001", "Can", 96, 1, 28m, 96, 24),
            Product("PRD-1010", "Canned Tuna Chunks 180g", "CAT-CAN", "SUP-003", "Can", 72, 1, 49m, 72, 18),
            Product("PRD-1011", "Tomato Sauce 250g", "CAT-CAN", "SUP-001", "Pack", 60, 1, 27m, 60, 15),
            Product("PRD-1012", "Sweet Corn Kernels 425g", "CAT-CAN", "SUP-003", "Can", 36, 1, 62m, 36, 9),
            Product("PRD-1013", "Baked Beans 420g", "CAT-CAN", "SUP-003", "Can", 30, 1, 59m, 30, 8),
            Product("PRD-1014", "Bottled Water 500ml", "CAT-BEV", "SUP-002", "Bottle", 120, 1, 18m, 120, 30),
            Product("PRD-1015", "Orange Juice 1L", "CAT-BEV", "SUP-003", "Carton", 24, 1, 95m, 24, 6),
            Product("PRD-1016", "Instant Coffee Sachets", "CAT-BEV", "SUP-001", "Box", 20, 10, 135m, 200, 50),
            Product("PRD-1017", "Chocolate Drink Powder 400g", "CAT-BEV", "SUP-001", "Pack", 24, 1, 112m, 24, 6),
            Product("PRD-1018", "Potato Chips 60g", "CAT-SNK", "SUP-002", "Pack", 50, 1, 35m, 50, 12),
            Product("PRD-1019", "Cheese Crackers 200g", "CAT-SNK", "SUP-002", "Pack", 40, 1, 48m, 40, 10),
            Product("PRD-1020", "Oat Biscuits 250g", "CAT-SNK", "SUP-003", "Pack", 32, 1, 65m, 32, 8)
        };

        foreach (var product in products)
        {
            product.CategoryId = categoryIds[product.Description!.Split('|')[0]];
            product.SupplierId = supplierIds[product.Description.Split('|')[1]];
            product.Description = $"Sample catalogue item: {product.ProductName}";
            product.ExpirationDate = expiration;
        }

        var existingProductCodes = await context.Products.Select(product => product.ProductCode).ToListAsync();
        var newProducts = products.Where(product => !existingProductCodes.Contains(product.ProductCode)).ToList();
        context.Products.AddRange(newProducts);
        await context.SaveChangesAsync();

        var newProductIds = newProducts.Select(product => product.Id).ToList();
        var inventoriedProductIds = await context.Inventories
            .Where(inventory => newProductIds.Contains(inventory.ProductId))
            .Select(inventory => inventory.ProductId)
            .ToListAsync();
        context.Inventories.AddRange(newProducts
            .Where(product => !inventoriedProductIds.Contains(product.Id))
            .Select(product => new Inventory
            {
                ProductId = product.Id,
                StockQuantity = product.StockQuantity,
                ReorderLevel = product.ReorderLevel,
                StockStatus = "In Stock",
                LastUpdated = DateTime.Now,
                ExpirationDate = product.ExpirationDate
            }));
        await context.SaveChangesAsync();
    }

    private static Product Product(string code, string name, string categoryCode, string supplierCode, string unit,
        int boxes, int piecesPerBox, decimal price, int stock, int reorderLevel) => new()
    {
        ProductCode = code,
        ProductName = name,
        Description = $"{categoryCode}|{supplierCode}",
        Unit = unit,
        Boxes = boxes,
        PiecesPerBox = piecesPerBox,
        Price = price,
        StockQuantity = stock,
        ReorderLevel = reorderLevel
    };
}
