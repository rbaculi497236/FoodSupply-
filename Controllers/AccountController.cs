using FoodSupply.Services;
using Microsoft.AspNetCore.RateLimiting;
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
[EnableRateLimiting("account")]
public class AccountController : Controller
{
private readonly ApplicationDbContext _context;
private readonly PasswordRecoveryService _recovery;
private readonly PasswordHasher<User> _passwordHasher;

    public AccountController(ApplicationDbContext context, PasswordRecoveryService recovery)
    {
        _context = context;
        _recovery = recovery;
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
            new Claim("SecurityStamp", user.SecurityStamp),
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

        await _recovery.RequestAsync(model.Email);
        ViewBag.Message = "If an active account matches that email, a password reset link will be sent. Check your inbox or contact your administrator.";
        return View(model);
    }
    // ==============================
    // RESET PASSWORD - GET
    // ==============================

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int userId, string token)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsArchived);
        if (user == null || !PasswordRecoveryService.Valid(user, token))
            return BadRequest("This password reset link is invalid or expired. Request a new link.");
        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (!await _recovery.ResetAsync(model.UserId, model.Token, model.NewPassword))
        {
            ModelState.AddModelError("", "This password reset link is invalid or expired. Request a new link.");
            return View(model);
        }
        TempData["Success"] = "Password reset successfully. You can now log in.";
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
