namespace RealTimeInventorySystem.Models;

public record OrderRequest(
    string OrderId,
    int CustomerId,
    decimal Amount);
