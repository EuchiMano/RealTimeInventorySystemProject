using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Services;
using System.Diagnostics;

namespace RealTimeInventorySystem.Controllers;

// Parte 6: Observabilidad — Logs + Traces + Métricas
//
// Los tres pilares:
//   Logs    → ILogger con campos estructurados + CorrelationId (del middleware)
//   Traces  → Stopwatch por span, Activity para OpenTelemetry
//   Métricas → AppMetrics (Counter + Histogram vía System.Diagnostics.Metrics)
//
// Escenario: un usuario reporta "el checkout tardó 18 segundos"
// Con esta observabilidad podés identificar exactamente qué span fue lento
//
// Endpoint: POST /api/observability-demo/checkout
[ApiController]
[Route("api/observability-demo")]
public class ObservabilityDemoController : ControllerBase
{
    private readonly AppMetrics _metrics;
    private readonly ILogger<ObservabilityDemoController> _logger;

    public ObservabilityDemoController(
        AppMetrics metrics,
        ILogger<ObservabilityDemoController> logger)
    {
        _metrics = metrics;
        _logger  = logger;
    }

    // Simula un checkout con logs estructurados, spans y métricas
    // El CorrelationId ya viene del middleware en X-Correlation-Id
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] ObsCheckoutRequest request)
    {
        var correlationId = HttpContext.Items["X-Correlation-Id"]?.ToString() ?? "unknown";
        var totalSw       = Stopwatch.StartNew();

        _metrics.CheckoutsStarted.Add(1);

        _logger.LogInformation(
            "Checkout iniciado. CorrelationId={CorrelationId} OrderId={OrderId} UserId={UserId} Amount={Amount}",
            correlationId, request.OrderId, request.UserId, request.Amount);

        var spans = new List<SpanResult>();

        // ── Span 1: CreateOrder ───────────────────────────────────────────────
        spans.Add(await RunSpanAsync("CreateOrder", async () =>
        {
            await Task.Delay(30);
            _logger.LogInformation(
                "Orden {OrderId} creada. CorrelationId={CorrelationId}",
                request.OrderId, correlationId);
        }));

        // ── Span 2: ReserveInventory ──────────────────────────────────────────
        spans.Add(await RunSpanAsync("ReserveInventory", async () =>
        {
            await Task.Delay(50);
            _logger.LogInformation(
                "Inventario reservado para {OrderId}. CorrelationId={CorrelationId}",
                request.OrderId, correlationId);
        }));

        // ── Span 3: ChargePayment (el lento) ─────────────────────────────────
        spans.Add(await RunSpanAsync("ChargePayment", async () =>
        {
            // Simula latencia alta si amount > 500
            var delay = request.Amount > 500 ? 400 : 60;
            await Task.Delay(delay);
            _logger.LogInformation(
                "Pago de {Amount} procesado. CorrelationId={CorrelationId} ElapsedMs={Elapsed}",
                request.Amount, correlationId, delay);
        }));

        // ── Span 4: SendEmail ─────────────────────────────────────────────────
        spans.Add(await RunSpanAsync("SendEmail", async () =>
        {
            await Task.Delay(20);
            _logger.LogInformation(
                "Email enviado a UserId={UserId}. CorrelationId={CorrelationId}",
                request.UserId, correlationId);
        }));

        totalSw.Stop();
        var totalMs = totalSw.Elapsed.TotalMilliseconds;

        // ── Métricas ──────────────────────────────────────────────────────────
        _metrics.CheckoutsCompleted.Add(1);
        _metrics.CheckoutDurationMs.Record(totalMs);

        _logger.LogInformation(
            "Checkout completado. CorrelationId={CorrelationId} OrderId={OrderId} TotalMs={TotalMs:F1}",
            correlationId, request.OrderId, totalMs);

        return Ok(new
        {
            CorrelationId = correlationId,
            request.OrderId,
            TotalMs       = Math.Round(totalMs, 1),
            Spans         = spans,
            Hint          = request.Amount > 500
                ? "ChargePayment fue el span lento (amount > 500 simula latencia alta)."
                : "Todos los spans con latencia normal."
        });
    }

    private static async Task<SpanResult> RunSpanAsync(string name, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        return new SpanResult(name, Math.Round(sw.Elapsed.TotalMilliseconds, 1));
    }
}

public record ObsCheckoutRequest(string OrderId, string UserId, decimal Amount);
public record SpanResult(string Span, double ElapsedMs);
