using FoodSupply.Services;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using FoodSupply.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// MVC
// ==========================================

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<SalesOrderService>();
builder.Services.AddScoped<OperationsService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PasswordRecoveryService>();
builder.Services.AddScoped<IRecoveryEmailSender, SmtpRecoveryEmailSender>();
builder.Services.AddControllersWithViews(options => options.Filters.Add<MutationFilter>());
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ==========================================
// DATABASE
// ==========================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    ));

// ==========================================
// COOKIE AUTHENTICATION
// ==========================================

builder.Services.AddAuthentication(
    CookieAuthenticationDefaults.AuthenticationScheme
)
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context => {
        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(id, out var userId) ? await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId) : null;
        if (user == null || !user.IsActive || user.IsArchived ||
            user.SecurityStamp != context.Principal?.FindFirstValue("SecurityStamp") ||
            user.Role != context.Principal?.FindFirstValue(ClaimTypes.Role)) {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
});

// ==========================================
// AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();

var app = builder.Build();

if (args.Contains("--upgrade-database"))
{
    using var scope = app.Services.CreateScope();
    await DatabaseUpgrade.RunAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    return;
}

// Check migration history before authentication or any query against the new model.
// Do not silently change a business database during ordinary startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    if (pending.Length > 0)
    {
        app.Logger.LogError("Database upgrade required. Pending migrations: {Migrations}. Stop the app, back up the database, then run dotnet run -- --upgrade-database.", string.Join(", ", pending));
        app.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(app.Environment.IsDevelopment()
                ? "FoodSupply needs a database upgrade. Stop the app, back up the database, run: dotnet run -- --upgrade-database, then start the app again."
                : "FoodSupply is temporarily unavailable while a database upgrade is required. Please contact the administrator.");
        });
        app.Run();
        return;
    }
}

// Populate a useful starter catalogue the first time the application runs.
// The seeder only inserts records whose codes do not already exist.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedSampleData"))
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseSeeder.SeedSampleProductsAsync(context);
}

// ==========================================
// ERROR HANDLING
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ==========================================
// HTTPS
// ==========================================

app.UseHttpsRedirection();

// ==========================================
// STATIC FILES
// ==========================================

app.UseStaticFiles();

// ==========================================
// ROUTING
// ==========================================

app.UseRouting();
app.UseRateLimiter();

// ==========================================
// AUTHENTICATION
// ==========================================

app.UseAuthentication();

app.UseAuthorization();

// ==========================================
// DEFAULT ROUTE
// ==========================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();
