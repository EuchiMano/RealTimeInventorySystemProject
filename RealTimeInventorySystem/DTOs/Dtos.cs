namespace RealTimeInventorySystem.DTOs;

public class UpdateStockRequest
{
    public long WarehouseId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = "Manual adjustment";
    public long Version { get; set; }
    // Optional idempotency/reference token to avoid duplicate movements
    public string? ReferenceNumber { get; set; }
    public string? UserId { get; set; }
    public string? Notes { get; set; }
}

public class InventoryResponse
{
    public long InventoryId { get; set; }
    public long ProductId { get; set; }
    public long WarehouseId { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public long Version { get; set; }
    public DateTime LastUpdated { get; set; }
}

