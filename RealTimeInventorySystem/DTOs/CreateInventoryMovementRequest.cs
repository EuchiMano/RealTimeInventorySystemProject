namespace RealTimeInventorySystem.DTOs;
public class CreateInventoryMovementRequest
{
    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Reason { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? UserId { get; set; }

    public string? Notes { get; set; }
}