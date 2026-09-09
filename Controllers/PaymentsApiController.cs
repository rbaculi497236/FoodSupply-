using FoodSupply.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize(Roles = "Admin,Manager,Main Admin,Sales Staff / Billing Staff,Sales/Customer Staff,Billing Staff")]
public class PaymentsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PaymentsApiController(ApplicationDbContext context) => _context = context;

    [HttpPost("{billingId:int}")]
    public async Task<IActionResult> RecordPayment(int billingId, PaymentRequest request)
    {
        var billing = await _context.Billings.FirstOrDefaultAsync(b => b.Id == billingId && !b.IsArchived);
        if (billing == null) return NotFound(new { message = "Billing record not found." });
        if (request.Amount <= 0 || billing.AmountPaid + request.Amount > billing.TotalAmount)
            return BadRequest(new { message = "Payment must be positive and cannot exceed the remaining balance." });

        billing.AmountPaid += request.Amount;
        billing.Balance = billing.TotalAmount - billing.AmountPaid;
        billing.PaymentMethod = request.PaymentMethod;
        billing.PaymentDate = DateTime.Now;
        billing.PaymentStatus = billing.Balance == 0 ? "Paid" : "Partially Paid";
        await _context.SaveChangesAsync();

        return Ok(new { billing.Id, billing.InvoiceNumber, billing.AmountPaid, billing.Balance, billing.PaymentStatus });
    }

    public sealed class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
    }
}