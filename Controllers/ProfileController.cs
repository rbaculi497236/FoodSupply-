using System.Security.Claims;
using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ProfileController(ApplicationDbContext db) : Controller
{
    private Task<User?> CurrentUserAsync() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive && !u.IsArchived)
        : Task.FromResult<User?>(null);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await CurrentUserAsync();
        return user == null ? Challenge() : View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await CurrentUserAsync();
        return user == null ? Challenge() : View(new ProfileViewModel {
            FullName = user.FullName, Username = user.Username, Email = user.Email });
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("account")]
    public async Task<IActionResult> Edit(ProfileViewModel model)
    {
        var user = await CurrentUserAsync();
        if (user == null) return Challenge();
        if (!ModelState.IsValid) return View(model);

        var hasher = new PasswordHasher<User>();
        var validPassword = false;
        try { validPassword = hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) != PasswordVerificationResult.Failed; }
        catch (FormatException) { }
        if (!validPassword)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Your current password is incorrect.");
            return View(model);
        }

        var username = model.Username.Trim();
        var email = model.Email.Trim();
        if (await db.Users.AnyAsync(u => u.Id != user.Id && u.Username.ToUpper() == username.ToUpper()))
            ModelState.AddModelError(nameof(model.Username), "Username already exists.");
        if (await db.Users.AnyAsync(u => u.Id != user.Id && u.Email.ToUpper() == email.ToUpper()))
            ModelState.AddModelError(nameof(model.Email), "Email already exists.");
        if (!ModelState.IsValid) return View(model);

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            user.ResetTokenHash = null;
            user.ResetTokenExpiresAt = null;
        }
        user.FullName = model.FullName.Trim();
        user.Username = username;
        user.Email = email;
        // Invalidate other sessions that still contain the old profile details.
        user.SecurityStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync();

        var authentication = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaims(new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role), new Claim("FullName", user.FullName),
            new Claim("SecurityStamp", user.SecurityStamp)
        });
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), authentication.Properties ?? new AuthenticationProperties());
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }
}
