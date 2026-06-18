using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Models;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 2: Saga Pattern — Orquestación de pasos con compensación automática
//
// Escenario: Checkout de e-commerce
//   Crear Orden → Reservar Inventario → Cobrar Tarjeta → Enviar Confirmación
//
// Si el cobro falla:
//   Compensar: Liberar Inventario → Cancelar Orden
//
// Endpoint: POST /api/saga-demo/checkout
[ApiController]
[Route("api/saga-demo")]
public class SagaDemoController : ControllerBase
{
    private readonly CheckoutSagaService _saga;

    public SagaDemoController(CheckoutSagaService saga) => _saga = saga;

    // Ejecuta la saga completa
    // Si amount > 1000 → simula fallo en el cobro → compensa automáticamente
    // Si amount ≤ 1000 → completa exitosamente
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] SagaCheckoutRequest request)
    {
        var saga = await _saga.RunAsync(request.OrderId, request.CustomerId, request.Amount);

        return Ok(new
        {
            saga.SagaId,
            saga.OrderId,
            saga.Amount,
            Status          = saga.Status.ToString(),
            saga.FailureReason,
            CompletedSteps  = saga.CompletedSteps,
            CompensatedSteps = saga.CompensatedSteps,
            Hint = saga.Status == SagaStatus.Compensated
                ? "Fallo simulado: amount > 1000 activa rechazo de tarjeta."
                : "Éxito: amount ≤ 1000."
        });
    }

    // Consulta el estado de una saga ya ejecutada
    [HttpGet("{sagaId:guid}")]
    public IActionResult GetSaga(Guid sagaId)
    {
        var saga = _saga.Get(sagaId);

        if (saga is null)
            return NotFound(new { Message = $"Saga {sagaId} no encontrada." });

        return Ok(new
        {
            saga.SagaId,
            saga.OrderId,
            Status           = saga.Status.ToString(),
            saga.FailureReason,
            CompletedSteps   = saga.CompletedSteps,
            CompensatedSteps = saga.CompensatedSteps
        });
    }
}

public record SagaCheckoutRequest(string OrderId, int CustomerId, decimal Amount);
