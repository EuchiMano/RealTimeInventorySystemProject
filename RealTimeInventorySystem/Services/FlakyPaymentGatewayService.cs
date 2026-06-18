using System.Collections.Concurrent;

namespace RealTimeInventorySystem.Services;

// Parte 3: Simula un gateway de pago que falla N veces antes de responder OK
// Permite demostrar retry, backoff y circuit breaker
public class FlakyPaymentGatewayService
{
    // Contador de fallos por clientId — persiste entre llamadas para simular el escenario real
    private readonly ConcurrentDictionary<string, int> _failCounts = new();
    private readonly ILogger<FlakyPaymentGatewayService> _logger;

    public FlakyPaymentGatewayService(ILogger<FlakyPaymentGatewayService> logger)
        => _logger = logger;

    // Falla las primeras `failTimes` llamadas y luego retorna OK
    public Task<string> ChargeAsync(string clientId, decimal amount, int failTimes = 3)
    {
        var count = _failCounts.AddOrUpdate(clientId, 1, (_, c) => c + 1);

        _logger.LogInformation("[FlakyGateway] Intento #{Attempt} para {ClientId}", count, clientId);

        if (count <= failTimes)
        {
            _logger.LogWarning("[FlakyGateway] Intento #{Attempt} → 500 Server Error", count);
            throw new HttpRequestException($"Payment gateway error 500 (attempt {count})");
        }

        var chargeId = $"charge-{Guid.NewGuid():N}";
        _logger.LogInformation("[FlakyGateway] Intento #{Attempt} → OK {ChargeId}", count, chargeId);

        // Resetea para que el demo se pueda repetir
        _failCounts.TryRemove(clientId, out _);

        return Task.FromResult(chargeId);
    }
}
