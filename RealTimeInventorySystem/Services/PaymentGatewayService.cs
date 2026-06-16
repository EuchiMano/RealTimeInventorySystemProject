using Microsoft.Extensions.Options;
using RealTimeInventorySystem.Options;

namespace RealTimeInventorySystem.Services;

// Parte 4C: IOptionsMonitor — singleton que reacciona a cambios de config en caliente
// Escenario: el proveedor rota la ApiKey sin necesidad de reiniciar la app
public class PaymentGatewayService
{
    private readonly IOptionsMonitor<PaymentGatewayOptions> _monitor;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(
        IOptionsMonitor<PaymentGatewayOptions> monitor,
        ILogger<PaymentGatewayService> logger)
    {
        _monitor = monitor;
        _logger  = logger;

        // Se suscribe a cambios: si appsettings.json cambia, este callback se dispara
        _monitor.OnChange(opts =>
            _logger.LogInformation(
                "PaymentGateway config updated. New endpoint: {Endpoint}",
                opts.Endpoint));
    }

    public async Task<string> ChargeAsync(decimal amount)
    {
        // Leemos CurrentValue en cada uso — no al construir el objeto
        // Esto garantiza que siempre usamos la config más reciente
        var config = _monitor.CurrentValue;

        _logger.LogInformation(
            "Charging {Amount} via {Endpoint} (timeout: {Timeout}s)",
            amount,
            config.Endpoint,
            config.TimeoutSeconds);

        await Task.Delay(50); // simula llamada HTTP

        return $"charged-{Guid.NewGuid():N}";
    }
}
