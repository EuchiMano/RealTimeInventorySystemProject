using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 4: Azure Service Bus — Topic + Subscriptions + DLQ
//
// Equivalencia con Azure Service Bus real:
//   InMemoryServiceBus     ↔  Topic "order-events"
//   Subscribe("Inventory") ↔  Subscription con filtro
//   Abandon() × 3         ↔  DeliveryCount → mensaje va a $DeadLetterQueue
//
// Flujo completo:
//   1. POST /api/servicebus-demo/publish   → publica OrderCreated a todos los suscriptores
//   2. GET  /api/servicebus-demo/receive/{subscriber} → el suscriptor consume su mensaje
//   3. POST /api/servicebus-demo/complete  → marca el mensaje como procesado (lo elimina)
//   4. POST /api/servicebus-demo/abandon   → falla la entrega (3 veces → DLQ)
//   5. GET  /api/servicebus-demo/dlq       → mensajes en Dead Letter Queue
[ApiController]
[Route("api/servicebus-demo")]
public class ServiceBusDemoController : ControllerBase
{
    private readonly InMemoryServiceBus _bus;

    public ServiceBusDemoController(InMemoryServiceBus bus) => _bus = bus;

    // Publica un evento OrderCreated — todos los suscriptores reciben su copia
    // Los tres suscriptores (Inventory, Billing, Notifications) están pre-registrados en DI
    [HttpPost("publish")]
    public IActionResult Publish([FromBody] PublishOrderRequest request)
    {
        var payload = $"{{\"orderId\":{request.OrderId},\"amount\":{request.Amount}}}";

        _bus.Publish("OrderCreated", payload);

        return Ok(new
        {
            Message   = "Evento OrderCreated publicado al topic.",
            Payload   = payload,
            Hint      = "Cada suscriptor (Inventory, Billing, Notifications) recibió su copia independiente."
        });
    }

    // El suscriptor lee su próximo mensaje pendiente
    // Si está caído (Notifications down) → su cola acumula mensajes, los otros no se ven afectados
    [HttpGet("receive/{subscriber}")]
    public IActionResult Receive(string subscriber)
    {
        var msg = _bus.Receive(subscriber);

        if (msg is null)
            return Ok(new { Message = $"No hay mensajes pendientes para '{subscriber}'." });

        return Ok(new
        {
            msg.MessageId,
            msg.EventType,
            msg.Payload,
            msg.EnqueuedAt,
            msg.DeliveryCount,
            Hint = $"Llamá a /complete o /abandon con este MessageId para '{subscriber}'."
        });
    }

    // El suscriptor procesó el mensaje correctamente → se elimina de su cola
    [HttpPost("complete")]
    public IActionResult Complete([FromBody] MessageActionRequest request)
    {
        _bus.Complete(request.Subscriber, request.MessageId);
        return Ok(new { Message = $"Mensaje {request.MessageId} completado por '{request.Subscriber}'." });
    }

    // El suscriptor falló al procesar → incrementa DeliveryCount
    // Tras 3 fallos → el mensaje va automáticamente al DLQ
    [HttpPost("abandon")]
    public IActionResult Abandon([FromBody] AbandonRequest request)
    {
        _bus.Abandon(request.Subscriber, request.MessageId, request.Reason);
        return Ok(new
        {
            Message = $"Mensaje abandonado por '{request.Subscriber}'.",
            Hint    = "Llamá /abandon 3 veces con el mismo MessageId para verlo ir al DLQ."
        });
    }

    // Muestra los mensajes en el Dead Letter Queue
    // En Azure: se accede como <topic>/subscriptions/<name>/$DeadLetterQueue
    [HttpGet("dlq")]
    public IActionResult GetDlq()
    {
        var messages = _bus.GetDeadLetterQueue();
        return Ok(new
        {
            Count    = messages.Count,
            Messages = messages,
            Hint     = "Estos mensajes requieren intervención manual o re-procesamiento."
        });
    }

    // Ver todos los mensajes pendientes de un suscriptor
    [HttpGet("pending/{subscriber}")]
    public IActionResult GetPending(string subscriber)
    {
        var messages = _bus.GetPending(subscriber);
        return Ok(new { Subscriber = subscriber, Count = messages.Count, Messages = messages });
    }
}

public record PublishOrderRequest(int OrderId, decimal Amount);
public record MessageActionRequest(string Subscriber, Guid MessageId);
public record AbandonRequest(string Subscriber, Guid MessageId, string Reason);
