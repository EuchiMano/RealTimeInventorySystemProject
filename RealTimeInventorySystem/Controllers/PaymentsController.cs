using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using RealTimeInventorySystem.Services.Payments;

namespace RealTimeInventorySystem.Controllers;

// Ejercicio 1: POST /api/payments
// Llama al gateway simulado con fallas aleatorias (30% Timeout, 20% 500, 50% Success)
// Polly maneja reintentos y circuit breaker de forma transparente
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ResilientPaymentGatewayService _payment;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ResilientPaymentGatewayService payment,
        ILogger<PaymentsController> logger)
    {
        _payment = payment;
        _logger  = logger;
    }

    // POST /api/payments
    // El gateway falla aleatoriamente — Polly reintenta Timeout y 500 automáticamente
    // Revisá los logs del servidor para ver los reintentos con backoff y jitter
    //
    // Errores reintentados:   TimeoutException, HttpRequestException (500)
    // Errores NO reintentados: 400, 401, 409 — son errores permanentes del cliente
    [HttpPost]
    public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
    {
        if (request.Amount <= 0)
            // 400 Bad Request — error de cliente, Polly no reintenta esto
            return BadRequest(new
            {
                Message = "Amount debe ser mayor a 0.",
                Hint    = "Los 400 no se reintentan — el problema está en el request, no en el gateway."
            });

        _logger.LogInformation(
            "[Payments] Iniciando pago OrderId={OrderId} Amount={Amount}",
            request.OrderId, request.Amount);

        try
        {
            var chargeId = await _payment.ProcessPaymentAsync(request.OrderId, request.Amount);

            return Ok(new
            {
                ChargeId  = chargeId,
                request.OrderId,
                request.Amount,
                Message   = "Pago procesado exitosamente.",
                Hint      = "Revisá los logs para ver si Polly hizo reintentos antes de llegar aquí."
            });
        }
        catch (BrokenCircuitException)
        {
            return StatusCode(503, new
            {
                Message = "Servicio de pagos no disponible — Circuit Breaker abierto.",
                Hint    = "El circuito se abre tras 5 fallos en 30s. Esperá 15s para que pase a Half-Open.",
                Action  = "Informar al usuario que intente más tarde. No reintentar inmediatamente."
            });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new
            {
                Message = "Gateway timeout — se agotaron los reintentos.",
                Error   = ex.Message,
                Hint    = "Polly reintentó 3 veces con backoff exponencial + jitter y todos fallaron."
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new
            {
                Message = "Gateway error — se agotaron los reintentos.",
                Error   = ex.Message,
                Hint    = "Polly reintentó los 500. Si llegaste aquí, los 3 reintentos también fallaron."
            });
        }
    }
}

public record PaymentRequest(string OrderId, decimal Amount);
