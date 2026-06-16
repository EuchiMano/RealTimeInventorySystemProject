using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Models;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 5: Outbox Pattern — consistencia eventual ante fallos parciales
// Escenario: el cobro puede ocurrir pero la app cae antes de guardar la orden
// Solución: guardamos la orden + el evento en la misma "transacción" local
// Endpoint: POST /api/outbox-demo/place-order
[ApiController]
[Route("api/outbox-demo")]
public class OutboxDemoController : ControllerBase
{
    private readonly OutboxService _outbox;
    private readonly ILogger<OutboxDemoController> _logger;

    public OutboxDemoController(
        OutboxService outbox,
        ILogger<OutboxDemoController> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    // Flujo correcto con Outbox:
    // 1. Guardamos la orden (simulado)
    // 2. Guardamos el evento de cobro en el Outbox — misma "transacción"
    // 3. Respondemos 202 Accepted
    // 4. El OutboxProcessor (BackgroundService) procesa el cobro en background
    //
    // Si la app cae entre el paso 2 y el cobro real, el Outbox tiene el evento
    // y el worker lo reintentará cuando la app vuelva a arrancar
    [HttpPost("place-order")]
    public IActionResult PlaceOrder([FromBody] OrderRequest request)
    {
        _logger.LogInformation(
            "Saving order {OrderId} to database (simulated)...",
            request.OrderId);

        // Paso 1: guardar la orden en DB (simulado)
        // Paso 2: en la misma transacción, guardar el evento en el Outbox
        var outboxMessage = new OutboxMessage
        {
            Type    = "ChargeRequested",
            Payload = $"OrderId={request.OrderId} Amount={request.Amount} CustomerId={request.CustomerId}"
        };

        _outbox.Enqueue(outboxMessage);

        _logger.LogInformation(
            "OutboxMessage [{Id}] enqueued. Pending: {Count}",
            outboxMessage.Id,
            _outbox.PendingCount);

        // 202 Accepted: la orden fue aceptada, el cobro se procesará de forma asíncrona
        return Accepted(new
        {
            Message         = "Order accepted. Payment will be processed asynchronously.",
            OrderId         = request.OrderId,
            OutboxMessageId = outboxMessage.Id
        });
    }

    // Muestra cuántos mensajes quedan pendientes en el Outbox
    [HttpGet("pending")]
    public IActionResult GetPendingCount()
    {
        return Ok(new { PendingMessages = _outbox.PendingCount });
    }
}
