using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 3: Concurrencia optimista con ETag + If-Match
// Evita Lost Updates cuando dos usuarios modifican el mismo recurso
// Endpoint: GET/PUT /api/order-concurrency/{orderId}
[ApiController]
[Route("api/order-concurrency")]
public class OrderConcurrencyController : ControllerBase
{
    private readonly OrderConcurrencyService _service;

    public OrderConcurrencyController(OrderConcurrencyService service)
    {
        _service = service;
    }

    // GET devuelve la orden con su ETag en el header
    // El cliente debe guardar ese ETag y enviarlo en el PUT con If-Match
    [HttpGet("{orderId}")]
    public IActionResult GetOrder(string orderId)
    {
        var order = _service.Get(orderId);

        if (order is null)
            return NotFound();

        // ETag representa el estado actual del recurso
        Response.Headers.ETag = $"\"{order.ETag}\"";

        return Ok(new
        {
            order.OrderId,
            order.Status,
            order.Version
        });
    }

    // PUT requiere el header If-Match con el ETag obtenido en el GET
    // Si el ETag ya no coincide → otro usuario modificó el recurso → 412
    [HttpPut("{orderId}")]
    public IActionResult UpdateOrder(
        string orderId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] UpdateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
            return BadRequest(new { Message = "If-Match header is required." });

        // Quitamos las comillas del ETag si vienen así: "abc123" → abc123
        var etag = ifMatch.Trim('"');

        var success = _service.TryUpdate(
            orderId,
            request.Status,
            etag,
            out var updated);

        if (!success)
        {
            // 412 Precondition Failed: el ETag no coincide con la versión actual
            // Significa que alguien más actualizó el recurso primero
            return StatusCode(412, new
            {
                Message = "Resource was modified by another request. Fetch the latest version and retry."
            });
        }

        Response.Headers.ETag = $"\"{updated!.ETag}\"";

        return Ok(new
        {
            updated.OrderId,
            updated.Status,
            updated.Version
        });
    }
}

public record UpdateOrderRequest(string Status);
