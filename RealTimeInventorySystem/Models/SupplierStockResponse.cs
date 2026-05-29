namespace RealTimeInventorySystem.Services;

public class SupplierStockResponse
{
    public long SupplierId { get; set; }

    public int AvailableQuantity { get; set; }

    public DateTime RetrievedAt { get; set; }
}