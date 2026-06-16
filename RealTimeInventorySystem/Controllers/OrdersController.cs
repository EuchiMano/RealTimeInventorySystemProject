using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Models;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 2: Idempotency-Key — garantiza que la misma orden no se procese dos veces
// Endpoint: POST /api/orders
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IdempotencyService _idempotency;

    public OrdersController(IdempotencyService idempotency)
    {
        _idempotency = idempotency;
    }

    // Header requerido: Idempotency-Key: <uuid o string único>
    // Caso 1: clave nueva                      → 201 Created
    // Caso 2: misma clave, mismo body          → 200 con la respuesta original
    // Caso 3: misma clave, body distinto       → 409 Conflict
    [HttpPost]
    public IActionResult CreateOrder(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] OrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { Message = "Idempotency-Key header is required." });

        var existing = _idempotency.TryGet(idempotencyKey);

        if (existing is not null)
        {
            // Caso 3: misma clave pero body diferente
            if (!_idempotency.BodyMatches(existing, request))
                return Conflict(new
                {
                    Message = "Idempotency-Key already used with a different request body."
                });

            // Caso 2: duplicado exacto → devolvemos la misma respuesta original
            return Ok(existing.Response);
        }

        // Caso 1: primera vez que llega esta clave → procesamos la orden
        var response = new
        {
            OrderId    = request.OrderId,
            CustomerId = request.CustomerId,
            Amount     = request.Amount,
            Status     = "Created",
            CreatedAt  = DateTime.UtcNow
        };

        _idempotency.Store(idempotencyKey, request, response);

        return StatusCode(201, response);
    }
}
