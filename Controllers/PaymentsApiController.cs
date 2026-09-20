using FoodSupply.Data;
using FoodSupply.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace FoodSupply.Controllers;
[ApiController, Route("api/payments")]
[Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
public class PaymentsApiController(ApplicationDbContext db, PaymentService payments) : ControllerBase
{
    [HttpPost("{billingId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int billingId, PaymentRequest request)
    {
        if (!await db.Billings.AnyAsync(b => b.Id == billingId)) return NotFound();
        var payment = await payments.RecordAsync(billingId, request.Amount, request.PaymentMethod, request.Reference, request.RequestId);
        await db.SaveChangesAsync();
        return Ok(new { payment.Id, payment.BillingId, payment.Amount, payment.PaidAt });
    }
    public sealed class PaymentRequest
    {
        public decimal Amount { get; set; }
        [Required, StringLength(100)] public string PaymentMethod { get; set; } = "";
        [StringLength(200)] public string Reference { get; set; } = "";
        [Required, StringLength(100)] public string RequestId { get; set; } = "";
    }
}
