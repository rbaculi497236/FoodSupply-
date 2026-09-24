namespace FoodSupply.Models;

public class ReceiptViewModel
{
    public int OrderId { get; init; }
    public string Controller { get; init; } = "";
    public string OrderType { get; init; } = "";
    public string Number { get; init; } = "";
    public DateTime Date { get; init; }
    public string Status { get; init; } = "";
    public string PartyName { get; init; } = "";
    public string? PartyAddress { get; init; }
    public string? Notes { get; init; }
    public decimal Total { get; init; }
    public List<ReceiptLine> Items { get; init; } = [];
    public bool IsPurchase => Controller == "Purchases";
}

public record ReceiptLine(int Quantity, string Description, decimal UnitPrice, decimal Amount);
