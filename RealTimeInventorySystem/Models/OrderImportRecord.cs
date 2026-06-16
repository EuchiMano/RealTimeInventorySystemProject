namespace RealTimeInventorySystem.Models;

public record OrderImportRecord(
    DateTime Timestamp,
    string OrderId,
    string UserId,
    decimal Amount);
