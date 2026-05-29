namespace RealTimeInventorySystem.DTOs;
public class InventoryMovementResponse
{
    public long MovementId { get; set; }

    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}