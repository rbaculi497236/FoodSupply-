using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FoodSupply.Data;

// Generate and review migrations without opening the application's database.
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true).AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("DefaultConnection") ??
            "Server=localhost;Database=foodsupply;User=root;";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 0))).Options;
        return new ApplicationDbContext(options);
    }
}
