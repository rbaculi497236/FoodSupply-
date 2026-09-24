namespace FoodSupply.Models;

public class InventoryNotification
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public bool LowStock { get; set; }
    public bool Expiring { get; set; }
    public bool Quarantined { get; set; }
    public int SpoiledQuantity { get; set; }
    public int DamagedQuantity { get; set; }
}
