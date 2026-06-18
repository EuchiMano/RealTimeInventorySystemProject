namespace RealTimeInventorySystem.Models;

public enum SagaStatus
{
    Pending,
    InventoryReserved,
    PaymentFailed,
    Compensated,
    Completed
}

public class CheckoutSaga
{
    public Guid SagaId { get; init; } = Guid.NewGuid();
    public string OrderId { get; init; } = string.Empty;
    public int CustomerId { get; init; }
    public decimal Amount { get; init; }
    public SagaStatus Status { get; set; } = SagaStatus.Pending;
    public string? FailureReason { get; set; }
    public List<string> CompletedSteps { get; } = new();
    public List<string> CompensatedSteps { get; } = new();
}
