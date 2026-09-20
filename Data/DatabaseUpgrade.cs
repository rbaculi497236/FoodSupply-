using FoodSupply.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Data;

public static class DatabaseUpgrade
{
    public static async Task RunAsync(ApplicationDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        // Run before DDL because MySQL schema changes implicitly commit.
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'Inventories'";
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0)
            {
                command.CommandText = "SELECT COUNT(*) FROM (SELECT ProductId FROM Inventories GROUP BY ProductId HAVING COUNT(*) > 1) duplicates";
                if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0)
                    throw new InvalidOperationException("Upgrade stopped: reconcile duplicate inventory records per product before upgrading.");
                command.CommandText = "SELECT (SELECT COUNT(*) FROM Inventories WHERE StockQuantity < 0) + (SELECT COUNT(*) FROM Products WHERE StockQuantity < 0) + (SELECT COUNT(*) FROM Billings WHERE AmountPaid < 0 OR AmountPaid > TotalAmount)";
                if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0)
                    throw new InvalidOperationException("Upgrade stopped: reconcile negative stock or invalid paid balances before upgrading.");
            }
        }
        await db.Database.MigrateAsync();
        // The former user creation screen stored plaintext. Convert it once during the explicit upgrade,
        // rather than keeping a plaintext fallback in the login endpoint.
        var hasher = new PasswordHasher<User>();
        int converted = 0;
        foreach (var user in await db.Users.ToListAsync())
        {
            if (string.IsNullOrEmpty(user.PasswordHash)) continue;
            if (LooksHashed(user.PasswordHash)) continue;
            user.PasswordHash = hasher.HashPassword(user, user.PasswordHash);
            user.SecurityStamp = Guid.NewGuid().ToString();
            converted++;
        }
        db.AuditReason = "Explicit database upgrade: migrate legacy plaintext passwords";
        await db.SaveChangesAsync();
        Console.WriteLine($"Database upgraded. Migrated {converted} legacy passwords; existing passwords remain usable. Sign in again.");
    }

    public static bool LooksHashed(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            return (bytes.Length == 49 && bytes[0] == 0) || (bytes.Length >= 61 && bytes[0] == 1);
        }
        catch (FormatException) { return false; }
    }
}
