using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace RealTimeInventorySystem.Services;

// Parte 3: Wrappea FlakyPaymentGatewayService con una ResiliencePipeline de Polly
// Pipeline: Retry (3 reintentos, exponential backoff + jitter) → Circuit Breaker
public class ResilientPaymentService
{
    private readonly FlakyPaymentGatewayService _gateway;
    private readonly ResiliencePipeline<string> _pipeline;
    private readonly ILogger<ResilientPaymentService> _logger;

    public ResilientPaymentService(
        FlakyPaymentGatewayService gateway,
        ILogger<ResilientPaymentService> logger)
    {
        _gateway = gateway;
        _logger  = logger;

        _pipeline = new ResiliencePipelineBuilder<string>()

            // Capa 1: Retry con Exponential Backoff + Jitter
            // Esperas: ~1s, ~2s, ~4s (más jitter aleatorio para evitar thundering herd)
            .AddRetry(new RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromSeconds(1),
                UseJitter        = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[Polly] Retry #{Attempt} en {Delay:F1}s. Error: {Error}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })

            // Capa 2: Circuit Breaker
            // Se abre si ≥50% de las últimas 5 llamadas fallaron
            // Permanece abierto 15s antes de pasar a Half-Open
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                FailureRatio     = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration    = TimeSpan.FromSeconds(15),
                OnOpened = args =>
                {
                    logger.LogError("[Polly] Circuit ABIERTO por {Duration:F0}s", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("[Polly] Circuit CERRADO — servicio recuperado");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("[Polly] Circuit HALF-OPEN — probando...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<string> ChargeAsync(string clientId, decimal amount, int failTimes = 3)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _gateway.ChargeAsync(clientId, amount, failTimes));
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError("[Polly] Circuit abierto — rechazando petición sin intentar");
            throw;
        }
    }
}
