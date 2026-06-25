using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Services.Idempotency;

namespace RealTimeInventorySystem.Controllers;

// Ejercicio 2: Idempotencia en consumidor de Service Bus
//
// Simula el escenario completo:
//   1. Mensaje llega por primera vez → se procesa y se guarda el MessageId
//   2. App crashea → Service Bus reentrega el mismo mensaje (lock expiró)
//   3. El consumidor detecta el MessageId duplicado → skip sin reprocesar
//
// En producción el consumidor estaría en un BackgroundService leyendo del Service Bus.
// Este controller expone el mismo comportamiento via HTTP para poder demostrarlo.
[ApiController]
[Route("api/idempotency-demo")]
public class IdempotencyDemoController : ControllerBase
{
    private readonly IdempotentInventoryProcessor _processor;
    private readonly InMemoryProcessedMessageStore _store;

    public IdempotencyDemoController(
        IdempotentInventoryProcessor processor,
        InMemoryProcessedMessageStore store)
    {
        _processor = processor;
        _store     = store;
    }

    // Simula la llegada de un mensaje OrderCreated desde Service Bus
    // Primera llamada con el mismo MessageId → procesa la reserva
    // Segunda llamada con el mismo MessageId → detecta duplicado → skip
    //
    // Para simular el crash: llamá este endpoint dos veces con el mismo messageId
    [HttpPost("process-message")]
    public async Task<IActionResult> ProcessMessage([FromBody] IncomingMessage message)
    {
        var result = await _processor.ProcessAsync(
            message.MessageId,
            message.OrderId,
            message.Quantity);

        if (result.Skipped)
        {
            // Complete el mensaje sin reprocesar
            // En Azure Service Bus: receiver.CompleteMessageAsync(msg)
            return Ok(new
            {
                Action        = "SKIPPED — Complete sin reprocesar",
                result.MessageId,
                result.OrderId,
                result.Reason,
                Hint          = "Esto es lo que ocurre cuando Service Bus reentrega tras un crash. El inventario NO se duplica."
            });
        }

        return Ok(new
        {
            Action        = "PROCESSED — reserva guardada + MessageId almacenado",
            result.MessageId,
            result.OrderId,
            result.Reason,
            Hint          = "Llamá este endpoint de nuevo con el mismo MessageId para simular la reentrega tras crash."
        });
    }

    // Muestra todos los MessageIds ya procesados — equivale a consultar la tabla ProcessedMessages en DB
    [HttpGet("processed-messages")]
    public IActionResult GetProcessedMessages()
    {
        var messages = _store.GetAll();

        return Ok(new
        {
            Count    = messages.Count,
            Messages = messages.Select(kv => new
            {
                MessageId   = kv.Key,
                ProcessedAt = kv.Value
            }),
            Hint = "En producción esta sería la tabla ProcessedMessages en SQL Server."
        });
    }

    // Simula el escenario completo del crash en una sola llamada:
    // Procesa el mensaje, luego intenta procesarlo de nuevo (como si Service Bus lo reentregara)
    [HttpPost("simulate-crash-scenario")]
    public async Task<IActionResult> SimulateCrash([FromBody] IncomingMessage message)
    {
        // Primera entrega — procesamiento normal
        var firstResult = await _processor.ProcessAsync(
            message.MessageId, message.OrderId, message.Quantity);

        // Simula el crash y la reentrega del Service Bus con el mismo mensaje
        var secondResult = await _processor.ProcessAsync(
            message.MessageId, message.OrderId, message.Quantity);

        return Ok(new
        {
            Scenario = "Crash + reentrega simulados",
            PrimeraEntrega = new
            {
                Action = firstResult.Skipped ? "SKIPPED" : "PROCESSED",
                firstResult.Reason
            },
            SegundaEntrega = new
            {
                Action = secondResult.Skipped ? "SKIPPED — duplicado detectado" : "PROCESSED",
                secondResult.Reason
            },
            Conclusion = "La segunda entrega fue ignorada. El inventario se reservó exactamente una vez.",
            KeyConcept = "MessageId como clave de idempotencia — at-least-once delivery + consumidor idempotente = effectively exactly-once"
        });
    }
}

public record IncomingMessage(string MessageId, string OrderId, int Quantity);
