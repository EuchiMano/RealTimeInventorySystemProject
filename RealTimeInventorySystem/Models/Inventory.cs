namespace RealTimeInventorySystem.Models;

public class Inventory
{
    public long InventoryId { get; set; }
    public long ProductId { get; set; }
    public long WarehouseId { get; set; }
    public int Quantity { get; set; } = 0;
    public int ReservedQuantity { get; set; } = 0;
    public long Version { get; set; } = 1;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product Product { get; set; }
    public Warehouse Warehouse { get; set; }

    // Computed property
    public int AvailableQuantity => Quantity - ReservedQuantity;
}
