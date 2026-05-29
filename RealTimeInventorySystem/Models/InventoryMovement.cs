namespace RealTimeInventorySystem.Models;

public class InventoryMovement
{
    public long MovementId { get; set; }
    public long ProductId { get; set; }
    public long WarehouseId { get; set; }
    public string MovementType { get; set; } // IN, OUT, ADJUSTMENT, TRANSFER
    public int Quantity { get; set; }
    public string Reason { get; set; }
    public string ReferenceNumber { get; set; }
    public string UserId { get; set; }
    public string Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product Product { get; set; }
    public Warehouse Warehouse { get; set; }
}
