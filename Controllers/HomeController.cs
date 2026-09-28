using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FoodSupply.Models;

namespace FoodSupply.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult CookiePreferences(bool allowOptional, string? returnUrl)
    {
        var consent = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ITrackingConsentFeature>();
        if (allowOptional) consent?.GrantConsent();
        else consent?.WithdrawConsent();
        Response.Cookies.Append("FoodSupply.CookiePreference", allowOptional ? "all" : "essential",
            new CookieOptions { HttpOnly = true, IsEssential = true, Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax, Path = "/", MaxAge = TimeSpan.FromDays(180) });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Home/Privacy");
    }

    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true
    )]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id
                ?? HttpContext.TraceIdentifier
        });
    }
}
