using System.Data;
using FoodSupply.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace FoodSupply.Services;

// A business operation, including its audit entries, commits as one transaction.
// Serializable isolation protects read/check/write workflows and duplicate requests.
public sealed class MutationFilter(ApplicationDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) { await next(); return; }
        if (!context.ModelState.IsValid && context.Controller is ControllerBase controller &&
            controller is FoodSupply.Controllers.OperationsController)
        { context.Result = new BadRequestObjectResult(context.ModelState); return; }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await next();
        if (result.Exception != null)
        {
            await transaction.RollbackAsync();
            var exception = result.Exception;
            if (exception is DbUpdateConcurrencyException ||
                exception is MySqlException { Number: 1213 or 1205 or 1062 } ||
                exception is DbUpdateException { InnerException: MySqlException { Number: 1213 or 1205 or 1062 } })
            {
                result.ExceptionHandled = true;
                result.Result = new ConflictObjectResult("This record changed or the request was already submitted. Refresh the page before trying again.");
            }
            else if (exception is InvalidOperationException { Source: "FoodSupply.Business" })
            {
                result.ExceptionHandled = true;
                result.Result = new BadRequestObjectResult(exception.Message);
            }
            return;
        }
        if (!context.ModelState.IsValid || result.Result is ObjectResult { StatusCode: >= 400 } ||
            result.Result is StatusCodeResult { StatusCode: >= 400 })
            await transaction.RollbackAsync();
        else await transaction.CommitAsync();
    }
}

public static class BusinessRule
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message) { Source = "FoodSupply.Business" };
    }
}
