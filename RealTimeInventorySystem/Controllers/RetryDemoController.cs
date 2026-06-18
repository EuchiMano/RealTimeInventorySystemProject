using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 3: Diseño de Reintentos con Polly
//
// FlakyPaymentGatewayService simula un gateway que falla N veces antes de responder OK
// ResilientPaymentService lo wrappea con:
//   - Retry: 3 reintentos, exponential backoff + jitter
//   - Circuit Breaker: se abre si ≥50% de llamadas fallan en 30s
//
// Endpoints: POST /api/retry-demo/charge
[ApiController]
[Route("api/retry-demo")]
public class RetryDemoController : ControllerBase
{
    private readonly ResilientPaymentService _payment;
    private readonly ILogger<RetryDemoController> _logger;

    public RetryDemoController(
        ResilientPaymentService payment,
        ILogger<RetryDemoController> logger)
    {
        _payment = payment;
        _logger  = logger;
    }

    // Cobro con reintentos automáticos
    // failTimes: cuántas veces debe fallar el gateway antes de responder OK
    // clientId:  clave de sesión para el contador de fallos (usa uno distinto para cada demo)
    //
    // Ejemplo:
    //   failTimes=3 → 3 fallos (500) + 1 éxito (200) — Polly hace 3 reintentos → OK
    //   failTimes=4 → 4 fallos → supera MaxRetryAttempts=3 → falla definitivamente
    [HttpPost("charge")]
    public async Task<IActionResult> Charge([FromBody] RetryChargeRequest request)
    {
        _logger.LogInformation(
            "[RetryDemo] Iniciando cobro {Amount} para {ClientId}, failTimes={FailTimes}",
            request.Amount, request.ClientId, request.FailTimes);

        try
        {
            var chargeId = await _payment.ChargeAsync(
                request.ClientId,
                request.Amount,
                request.FailTimes);

            return Ok(new
            {
                ChargeId   = chargeId,
                Amount     = request.Amount,
                Message    = "Cobro exitoso tras reintentos automáticos.",
                Hint       = "Revisá los logs para ver los reintentos con backoff."
            });
        }
        catch (BrokenCircuitException)
        {
            return StatusCode(503, new
            {
                Message = "Circuit Breaker ABIERTO — el servicio está caído, no se intentó la llamada.",
                Hint    = "Esperá 15 segundos para que pase a Half-Open."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new
            {
                Message = "Gateway falló tras agotar todos los reintentos.",
                Error   = ex.Message,
                Hint    = "Intentá con failTimes ≤ 3 para que Polly logre recuperarse."
            });
        }
    }
}

public record RetryChargeRequest(
    string ClientId,
    decimal Amount,
    int FailTimes = 3);
