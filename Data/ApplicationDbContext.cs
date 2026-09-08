using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // User Management
        public DbSet<User> Users { get; set; }

        // Products
        public DbSet<Product> Products { get; set; }

        // Suppliers
        public DbSet<Supplier> Suppliers { get; set; }

        // Customers
        public DbSet<Customer> Customers { get; set; }

        // Categories
        public DbSet<Category> Categories { get; set; }

        // Inventory
        public DbSet<Inventory> Inventories { get; set; }

        // Purchasing
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }

        // Sales
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }

        // Deliveries
        public DbSet<Delivery> Deliveries { get; set; }

        // Billing
        public DbSet<Billing> Billings { get; set; }
    }
}