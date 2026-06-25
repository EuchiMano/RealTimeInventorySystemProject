namespace RealTimeInventorySystem.Services.Payments;

// Gateway simulado con fallas aleatorias según el escenario del ejercicio:
//   30% → Timeout         (1-3  de Random 1-10)
//   20% → HTTP 500        (4-5  de Random 1-10)
//   50% → Success         (6-10 de Random 1-10)
public class SimulatedPaymentGateway : IPaymentGateway
{
    private readonly ILogger<SimulatedPaymentGateway> _logger;

    public SimulatedPaymentGateway(ILogger<SimulatedPaymentGateway> logger)
        => _logger = logger;

    public async Task<string> ProcessPaymentAsync(string orderId, decimal amount)
    {
        await Task.Delay(50); // simula latencia base de red

        var roll = Random.Shared.Next(1, 11); // 1 a 10 inclusive

        _logger.LogInformation(
            "[Gateway] OrderId={OrderId} Amount={Amount} Roll={Roll}",
            orderId, amount, roll);

        if (roll <= 3)
        {
            // 30% Timeout — error transitorio, Polly debe reintentar
            _logger.LogWarning("[Gateway] → TIMEOUT (roll={Roll})", roll);
            throw new TimeoutException($"Gateway timeout procesando orden {orderId}.");
        }

        if (roll <= 5)
        {
            // 20% HTTP 500 — error transitorio, Polly debe reintentar
            _logger.LogWarning("[Gateway] → HTTP 500 (roll={Roll})", roll);
            throw new HttpRequestException(
                $"Gateway HTTP 500 - Internal Server Error para orden {orderId}.",
                inner: null,
                statusCode: System.Net.HttpStatusCode.InternalServerError);
        }

        // 50% éxito
        var chargeId = $"charge-{Guid.NewGuid():N}";
        _logger.LogInformation("[Gateway] → SUCCESS chargeId={ChargeId}", chargeId);
        return chargeId;
    }
}
