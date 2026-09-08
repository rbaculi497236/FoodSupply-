using FoodSupply.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// MVC
// ==========================================

builder.Services.AddControllersWithViews();

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


});

// ==========================================
// AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();

var app = builder.Build();

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

// ==========================================
// AUTHENTICATION
// ==========================================

app.UseAuthentication();

app.UseAuthorization();

// ==========================================
// MVC ROUTING
// ==========================================

app.MapControllerRoute(
name: "default",
pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();
