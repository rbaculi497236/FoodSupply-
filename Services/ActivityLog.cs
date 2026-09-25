using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace FoodSupply.Services;
// Runs outside the mutation transaction so rejected login attempts remain recorded.
public sealed class AccountActivityFilter(DbContextOptions<ApplicationDbContext> options, ILogger<AccountActivityFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        var action = context.RouteData.Values["action"]?.ToString();
        if (context.RouteData.Values["controller"]?.ToString() != "Account" || !HttpMethods.IsPost(context.HttpContext.Request.Method) || action is not ("Login" or "Logout")) return;
        var success = result.Exception == null && result.Result is RedirectToActionResult && context.ModelState.IsValid;
        var actor = context.HttpContext.Items["LoginUserId"]?.ToString() ?? context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        try
        {
            await using var auditDb = new ApplicationDbContext(options);
            auditDb.AuditEntries.Add(new AuditEntry { Actor = actor, Entity = "Account", RecordId = actor,
                Action = success ? action : action + "Failed", Reason = success ? (action == "Login" ? "Signed in" : "Signed out") : "Account access attempt rejected", Changes = "{}" });
            await auditDb.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogError("Could not save account activity ({ErrorType}).", ex.GetType().Name); }
    }
}
