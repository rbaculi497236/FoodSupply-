using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace FoodSupply.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? accessor = null)
            : base(options)
        {
            _accessor = accessor;
        }

        private readonly IHttpContextAccessor? _accessor;
        public string Actor => _accessor?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        public string AuditReason { get; set; } = "";
        public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
        public DbSet<DeliveryReceipt> DeliveryReceipts => Set<DeliveryReceipt>();
        public DbSet<CustomerReturn> CustomerReturns => Set<CustomerReturn>();
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

        public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            var ownsTransaction = Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
            // Inventory is authoritative; Product.StockQuantity is a read-only compatibility projection.
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<Inventory>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
            {
                if (entry.Entity.StockQuantity < 0) throw new InvalidOperationException("Stock cannot be negative.") { Source = "FoodSupply.Business" };
                var product = await Products.FindAsync(new object[] { entry.Entity.ProductId }, cancellationToken);
                if (product != null) product.StockQuantity = entry.Entity.StockQuantity;
            }
            ChangeTracker.DetectChanges();
            var changed = ChangeTracker.Entries().Where(e => e.Entity is not AuditEntry &&
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            foreach (var entry in changed)
                if (entry.Metadata.FindProperty("Version") != null)
                    entry.Property("Version").CurrentValue = Guid.NewGuid().ToString();
            var databaseValues = new Dictionary<object, Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues?>();
            foreach (var entry in changed.Where(e => e.State != EntityState.Added))
                databaseValues[entry.Entity] = await entry.GetDatabaseValuesAsync(cancellationToken);
            var audits = changed.Select(e => new { Entry = e, Audit = new AuditEntry {
                Actor = Actor, Entity = e.Metadata.ClrType.Name, Action = e.State.ToString(), Reason = AuditReason,
                Changes = JsonSerializer.Serialize(e.Properties.Where(p => !p.Metadata.IsPrimaryKey() &&
                    p.Metadata.Name is not ("PasswordHash" or "ResetTokenHash" or "ResetTokenExpiresAt" or "SecurityStamp" or "Version") &&
                    (e.State != EntityState.Modified || p.IsModified)).ToDictionary(p => p.Metadata.Name,
                        p => new { Before = e.State == EntityState.Added ? null : databaseValues.GetValueOrDefault(e.Entity)?[p.Metadata.Name],
                            After = e.State == EntityState.Deleted ? null : p.CurrentValue }))
            }}).ToList();
            var count = await base.SaveChangesAsync(true, cancellationToken);
            foreach (var item in audits)
            {
                item.Audit.RecordId = string.Join(",", item.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
                AuditEntries.Add(item.Audit);
            }
            if (audits.Count > 0) await base.SaveChangesAsync(true, cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return count;
        }

        // User Management
        public DbSet<User> Users { get; set; }

        // Products
        public DbSet<Product> Products { get; set; }

        // Suppliers
        public DbSet<Supplier> Suppliers { get; set; }

        // Customers
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerActivity> CustomerActivities { get; set; }

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

        // Customer Concerns
        public DbSet<CustomerConcern> CustomerConcerns { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Inventory>().HasIndex(i => i.ProductId).IsUnique();
            modelBuilder.Entity<Payment>().HasIndex(p => p.RequestId).IsUnique();
            modelBuilder.Entity<Payment>().HasIndex(p => p.ReversesPaymentId).IsUnique();
            modelBuilder.Entity<PurchaseReceipt>().HasIndex(p => p.RequestId).IsUnique();
            modelBuilder.Entity<DeliveryReceipt>().HasIndex(p => p.RequestId).IsUnique();
            modelBuilder.Entity<CustomerReturn>().HasIndex(p => p.RequestId).IsUnique();
            modelBuilder.Entity<InventoryBatch>().HasIndex(b => new { b.ProductId, b.ExpirationDate });
            modelBuilder.Entity<AuditEntry>().HasIndex(a => a.CreatedAt);
            foreach (var type in new[] { typeof(Inventory), typeof(InventoryBatch), typeof(Billing), typeof(SalesOrder),
                typeof(Purchase), typeof(PurchaseItem), typeof(Delivery), typeof(User), typeof(Product) })
                modelBuilder.Entity(type).Property<string>("Version").HasMaxLength(36).IsConcurrencyToken();
            modelBuilder.Entity<CustomerActivity>(entity =>
            {
                entity.ToTable("CustomerActivities");
                entity.HasKey(activity => activity.Id);
                entity.HasOne(activity => activity.Customer)
                    .WithMany()
                    .HasForeignKey(activity => activity.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
