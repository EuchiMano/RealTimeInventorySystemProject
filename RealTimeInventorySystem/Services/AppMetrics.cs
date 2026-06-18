using System.Diagnostics.Metrics;

namespace RealTimeInventorySystem.Services;

// Parte 6: Métricas con System.Diagnostics.Metrics (built-in .NET, sin librerías externas)
// Compatible con OpenTelemetry, Prometheus, Azure Monitor sin cambiar el código
public sealed class AppMetrics : IDisposable
{
    private readonly Meter _meter;

    // Counter: total de checkouts iniciados (solo sube, nunca baja)
    public readonly Counter<long> CheckoutsStarted;

    // Counter: checkouts completados vs fallidos
    public readonly Counter<long> CheckoutsCompleted;
    public readonly Counter<long> CheckoutsFailed;

    // Histogram: distribución de latencias (p50, p95, p99)
    public readonly Histogram<double> CheckoutDurationMs;

    // Counter: reintentos al gateway de pago
    public readonly Counter<long> PaymentRetries;

    public AppMetrics()
    {
        _meter = new Meter("RealTimeInventorySystem", "1.0.0");

        CheckoutsStarted   = _meter.CreateCounter<long>("checkouts.started",   "count", "Total checkouts iniciados");
        CheckoutsCompleted = _meter.CreateCounter<long>("checkouts.completed", "count", "Checkouts completados OK");
        CheckoutsFailed    = _meter.CreateCounter<long>("checkouts.failed",    "count", "Checkouts que fallaron");
        CheckoutDurationMs = _meter.CreateHistogram<double>("checkouts.duration_ms", "ms", "Duración del checkout en ms");
        PaymentRetries     = _meter.CreateCounter<long>("payment.retries",     "count", "Reintentos al gateway de pago");
    }

    public void Dispose() => _meter.Dispose();
}
