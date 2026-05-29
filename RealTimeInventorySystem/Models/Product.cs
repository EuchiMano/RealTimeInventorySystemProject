namespace RealTimeInventorySystem.Models;

public class Product
{
    public long ProductId { get; set; }
    public string ProductCode { get; set; }
    public string ProductName { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
