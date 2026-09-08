using System.Security.Claims;
using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers
{
public class AccountController : Controller
{
private readonly ApplicationDbContext _context;
private readonly PasswordHasher<User> _passwordHasher;

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    // ==============================
    // LOGIN - GET
    // ==============================

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    // ==============================
    // LOGIN - POST
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == model.Username);

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid username or password."
            );

            return View(model);
        }

        if (!user.IsActive || user.IsArchived)
        {
            ModelState.AddModelError(
                "",
                "This account is inactive or archived."
            );

            return View(model);
        }

        bool passwordValid = false;

        // ==========================================
        // CHECK PASSWORD
        // ==========================================

        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            try
            {
                var result = _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.Password
                );

                if (result == PasswordVerificationResult.Success ||
                    result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    passwordValid = true;

                    if (result ==
                        PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        user.PasswordHash =
                            _passwordHasher.HashPassword(
                                user,
                                model.Password
                            );

                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (FormatException)
            {
                passwordValid = false;
            }
        }

        // ==========================================
        // SUPPORT OLD PLAIN-TEXT PASSWORD
        // ==========================================

        if (!passwordValid &&
            user.PasswordHash == model.Password)
        {
            passwordValid = true;

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    model.Password
                );

            await _context.SaveChangesAsync();
        }

        // ==========================================
        // PASSWORD FAILED
        // ==========================================

        if (!passwordValid)
        {
            ModelState.AddModelError(
                "",
                "Invalid username or password."
            );

            return View(model);
        }

        // ==========================================
        // CREATE CLAIMS
        // ==========================================

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),

            new Claim(
                ClaimTypes.Name,
                user.Username
            ),

            new Claim(
                ClaimTypes.Email,
                user.Email
            ),

            new Claim(
                ClaimTypes.Role,
                user.Role
            ),

            new Claim(
                "FullName",
                user.FullName
            )
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        var principal = new ClaimsPrincipal(identity);

        // ==========================================
        // SIGN IN
        // ==========================================

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            }
        );

        // ==========================================
        // GO TO DASHBOARD
        // ==========================================

        return RedirectToAction("Index", "Dashboard");
    }

    // ==============================
    // LOGOUT
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction(nameof(Login));
    }

    // ==============================
    // FORGOT PASSWORD - GET
    // ==============================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    // ==============================
    // FORGOT PASSWORD - POST
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Email == model.Email &&
                u.IsActive &&
                !u.IsArchived
            );

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "No active account was found with that email."
            );

            return View(model);
        }

        return RedirectToAction(
            nameof(ResetPassword),
            new { userId = user.Id }
        );
    }

    // ==============================
    // RESET PASSWORD - GET
    // ==============================

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int userId)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || user.IsArchived)
        {
            return NotFound();
        }

        var model = new ResetPasswordViewModel
        {
            UserId = user.Id
        };

        ViewBag.Username = user.Username;

        return View(model);
    }

    // ==============================
    // RESET PASSWORD - POST
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == model.UserId);

        if (user == null || user.IsArchived)
        {
            return NotFound();
        }

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                model.NewPassword
            );

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Password reset successfully. You can now log in.";

        return RedirectToAction(nameof(Login));
    }

    // ==============================
    // ACCESS DENIED
    // ==============================

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}

}
