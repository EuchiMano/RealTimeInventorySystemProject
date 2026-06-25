using System.Collections.Concurrent;

namespace RealTimeInventorySystem.Services.Idempotency;

// Implementación in-memory del store de idempotencia
//
// En producción sería una tabla SQL:
//   CREATE TABLE ProcessedMessages (
//       MessageId   NVARCHAR(255) PRIMARY KEY,
//       ProcessedAt DATETIME2     NOT NULL DEFAULT GETUTCDATE()
//   )
//
// La operación de inserción en esa tabla debe ocurrir en la MISMA transacción
// que el UPDATE de inventario — si una falla, la otra hace rollback
// Esto garantiza que nunca quede un mensaje "procesado" sin inventario actualizado
// ni inventario actualizado con mensaje "no procesado"
public class InMemoryProcessedMessageStore : IProcessedMessageStore
{
    private readonly ConcurrentDictionary<string, DateTime> _processed = new();

    public Task<bool> IsAlreadyProcessedAsync(string messageId) =>
        Task.FromResult(_processed.ContainsKey(messageId));

    public Task MarkAsProcessedAsync(string messageId)
    {
        _processed[messageId] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    // Helper para el demo — muestra todos los mensajes procesados
    public IReadOnlyDictionary<string, DateTime> GetAll() => _processed;
}
