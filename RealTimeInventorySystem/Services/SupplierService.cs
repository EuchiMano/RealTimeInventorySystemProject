namespace RealTimeInventorySystem.Services;
public class SupplierService
{
    private readonly SemaphoreSlim _semaphore;

    private readonly ILogger<SupplierService> _logger;

    public SupplierService(
        ILogger<SupplierService> logger)
    {
        _logger = logger;

        // Maximum 5 concurrent calls
        _semaphore = new SemaphoreSlim(5, 5);
    }

    public async Task<SupplierStockResponse> GetSupplierStockAsync(
        long supplierId,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation(
                "Calling supplier API for supplier {SupplierId}",
                supplierId);

            // Simulates external API latency
            await Task.Delay(1000, cancellationToken);

            var stock = new SupplierStockResponse
            {
                SupplierId = supplierId,
                AvailableQuantity = Random.Shared.Next(1, 500),
                RetrievedAt = DateTime.UtcNow
            };

            return stock;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}