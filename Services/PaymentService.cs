using FoodSupply.Data;
using FoodSupply.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodSupply.Services;

public sealed class PaymentService(ApplicationDbContext db)
{
    public async Task<Payment> RecordAsync(int billingId, decimal amount, string method, string reference, string requestId)
    {
        BusinessRule.Require(!string.IsNullOrWhiteSpace(requestId) && requestId.Length <= 100, "A request ID is required.");
        var previous = await db.Payments.SingleOrDefaultAsync(p => p.RequestId == requestId);
        if (previous != null)
        {
            BusinessRule.Require(previous.BillingId == billingId && previous.Amount == amount &&
                previous.Method == method && previous.Reference == reference && previous.ReversesPaymentId == null,
                "This request ID belongs to a different payment.");
            return previous;
        }
        var billing = await db.Billings.SingleAsync(b => b.Id == billingId);
        BusinessRule.Require(!billing.IsArchived, "Restore the invoice before recording a payment.");
        BusinessRule.Require(amount > 0 && decimal.Round(amount, 2) == amount && amount <= billing.TotalAmount - billing.AmountPaid,
            "Payment must be positive, use at most two decimal places, and not exceed the balance.");
        BusinessRule.Require(!string.IsNullOrWhiteSpace(method) && method.Length <= 100 && reference.Length <= 200, "Enter a valid payment method and reference.");
        var payment = new Payment { Billing = billing, Amount = amount, Method = method,
            Reference = reference, RequestId = requestId, Actor = db.Actor };
        db.Payments.Add(payment);
        Update(billing, amount, method);
        return payment;
    }

    public async Task ReverseAsync(int paymentId, string reason, string requestId)
    {
        BusinessRule.Require(!string.IsNullOrWhiteSpace(reason), "A reversal reason is required.");
        BusinessRule.Require(!string.IsNullOrWhiteSpace(requestId) && requestId.Length <= 100, "A request ID is required.");
        var previous = await db.Payments.SingleOrDefaultAsync(p => p.RequestId == requestId);
        if (previous != null)
        {
            BusinessRule.Require(previous.ReversesPaymentId == paymentId && previous.Reason == reason, "Request ID already used.");
            return;
        }
        var original = await db.Payments.Include(p => p.Billing).SingleAsync(p => p.Id == paymentId);
        BusinessRule.Require(original.Amount > 0 && !await db.Payments.AnyAsync(p => p.ReversesPaymentId == paymentId), "This payment cannot be reversed again.");
        BusinessRule.Require(original.Billing!.AmountPaid >= original.Amount, "Reversal exceeds the recorded amount paid.");
        db.Payments.Add(new Payment { BillingId = original.BillingId, Amount = -original.Amount,
            Method = original.Method, Reference = original.Reference, RequestId = requestId,
            ReversesPaymentId = original.Id, Reason = reason, Actor = db.Actor });
        db.AuditReason = reason;
        Update(original.Billing!, -original.Amount, original.Method);
    }

    private static void Update(Billing billing, decimal amount, string method)
    {
        billing.AmountPaid += amount;
        billing.Balance = billing.TotalAmount - billing.AmountPaid;
        billing.PaymentStatus = billing.AmountPaid == 0 ? "Unpaid" : billing.Balance == 0 ? "Paid" : "Partially Paid";
        billing.PaymentDate = DateTime.UtcNow;
        billing.PaymentMethod = method;
    }
}
