using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace RealTimeInventorySystem.Services.Payments;

// Wrappea IPaymentGateway con una ResiliencePipeline de Polly
//
// Pipeline (orden importa — el timeout envuelve al retry que envuelve al circuit breaker):
//   1. Retry: 3 reintentos, exponential backoff (1s → 2s → 4s) + jitter
//      Solo reintenta: TimeoutException y HttpRequestException (500)
//      NO reintenta:   400, 401, 409 — son errores permanentes de cliente/negocio
//   2. Circuit Breaker: abre después de 5 fallos en 30s, permanece abierto 15s
public class ResilientPaymentGatewayService
{
    private readonly IPaymentGateway _gateway;
    private readonly ResiliencePipeline<string> _pipeline;
    private readonly ILogger<ResilientPaymentGatewayService> _logger;
    private readonly AppMetrics _metrics;

    public ResilientPaymentGatewayService(
        IPaymentGateway gateway,
        ILogger<ResilientPaymentGatewayService> logger,
        AppMetrics metrics)
    {
        _gateway = gateway;
        _logger  = logger;
        _metrics = metrics;

        _pipeline = new ResiliencePipelineBuilder<string>()

            // Capa 1: Retry — solo errores transitorios
            .AddRetry(new RetryStrategyOptions<string>
            {
                // ShouldHandle define explícitamente qué errores son reintentables
                // TimeoutException  → el gateway no respondió a tiempo (transitorio)
                // HttpRequestException → 500 del gateway (transitorio)
                // Cualquier otro (400, 401, 409, InvalidOperation) → NO se reintenta
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<TimeoutException>()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode == System.Net.HttpStatusCode.InternalServerError),

                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromSeconds(1),  // 1s → 2s → 4s
                UseJitter        = true,                     // evita Thundering Herd

                OnRetry = args =>
                {
                    _metrics.PaymentRetries.Add(1);
                    _logger.LogWarning(
                        "[Polly] Retry #{Attempt} en {Delay:F2}s. Error: {Error}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })

            // Capa 2: Circuit Breaker
            // Abre si ≥50% de las llamadas fallan en una ventana de 30s (mínimo 5 llamadas)
            // Permanece abierto 15s → pasa a Half-Open → prueba con una llamada
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                ShouldHandle = new PredicateBuilder<string>()
                    .Handle<TimeoutException>()
                    .Handle<HttpRequestException>(),

                FailureRatio      = 0.5,
                MinimumThroughput = 5,
                SamplingDuration  = TimeSpan.FromSeconds(30),
                BreakDuration     = TimeSpan.FromSeconds(15),

                OnOpened = args =>
                {
                    _logger.LogError(
                        "[CircuitBreaker] ABIERTO por {Duration:F0}s — gateway no disponible",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("[CircuitBreaker] CERRADO — gateway recuperado");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("[CircuitBreaker] HALF-OPEN — enviando llamada de prueba");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<string> ProcessPaymentAsync(string orderId, decimal amount)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _gateway.ProcessPaymentAsync(orderId, amount));
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError(
                "[CircuitBreaker] Llamada rechazada para OrderId={OrderId} — circuito abierto",
                orderId);
            throw;
        }
    }
}
