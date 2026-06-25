namespace RealTimeInventorySystem.Services.Idempotency;

// Consumidor idempotente del evento OrderCreated
//
// El problema sin idempotencia:
//   1. Recibe mensaje OrderId=123
//   2. Reserva inventario en DB
//   3. CRASH antes del Complete
//   4. Service Bus reentrega el mensaje (lock expiró)
//   5. Reserva inventario OTRA VEZ → duplicado
//
// La solución: verificar MessageId antes de procesar
//   Si ya está en ProcessedMessages → skip (Complete sin hacer nada)
//   Si no está → procesar + guardar MessageId (idealmente en la misma transacción)
public class IdempotentInventoryProcessor
{
    private readonly IProcessedMessageStore _store;
    private readonly ILogger<IdempotentInventoryProcessor> _logger;

    public IdempotentInventoryProcessor(
        IProcessedMessageStore store,
        ILogger<IdempotentInventoryProcessor> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task<ProcessResult> ProcessAsync(string messageId, string orderId, int quantity)
    {
        _logger.LogInformation(
            "[IdempotentProcessor] Recibido MessageId={MessageId} OrderId={OrderId}",
            messageId, orderId);

        // Paso 1: verificar si ya fue procesado
        if (await _store.IsAlreadyProcessedAsync(messageId))
        {
            _logger.LogWarning(
                "[IdempotentProcessor] DUPLICADO detectado — MessageId={MessageId} ya fue procesado. Skipping.",
                messageId);

            // Complete el mensaje sin reprocesar — es seguro ignorarlo
            return new ProcessResult(
                Skipped: true,
                MessageId: messageId,
                OrderId: orderId,
                Reason: "Mensaje ya procesado anteriormente. Duplicate detection activada.");
        }

        // Paso 2: procesar la reserva de inventario
        _logger.LogInformation(
            "[IdempotentProcessor] Procesando reserva: OrderId={OrderId} Quantity={Qty}",
            orderId, quantity);

        await Task.Delay(30); // simula escritura en DB

        // Paso 3: marcar como procesado en la MISMA transacción que la reserva
        // En producción: _db.InventoryReservations.Add(...) + _db.ProcessedMessages.Add(...) + SaveChangesAsync()
        await _store.MarkAsProcessedAsync(messageId);

        _logger.LogInformation(
            "[IdempotentProcessor] Completado — MessageId={MessageId} marcado como procesado",
            messageId);

        return new ProcessResult(
            Skipped: false,
            MessageId: messageId,
            OrderId: orderId,
            Reason: "Reserva de inventario procesada exitosamente.");
    }
}

public record ProcessResult(
    bool   Skipped,
    string MessageId,
    string OrderId,
    string Reason);
