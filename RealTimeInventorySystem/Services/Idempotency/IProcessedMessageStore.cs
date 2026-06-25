namespace RealTimeInventorySystem.Services.Idempotency;

// Tabla de idempotencia: guarda los MessageId ya procesados
// En producción: tabla SQL en la misma DB que el inventario
// Aquí: in-memory para el demo
public interface IProcessedMessageStore
{
    // Retorna true si el mensaje ya fue procesado anteriormente
    Task<bool> IsAlreadyProcessedAsync(string messageId);

    // Marca el mensaje como procesado — debe llamarse en la misma transacción que la operación de negocio
    Task MarkAsProcessedAsync(string messageId);
}
