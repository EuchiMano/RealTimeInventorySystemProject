namespace RealTimeInventorySystem.Models;

public class Warehouse
{
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; }
    public string Location { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
