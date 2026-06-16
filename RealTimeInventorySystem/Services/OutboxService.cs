using System.Collections.Concurrent;
using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Services;

// Parte 5: Outbox Pattern — garantiza que el evento de cobro no se pierda
// La idea: guardar la orden Y el evento en la misma "transacción" antes de cobrar
public class OutboxService
{
    // En producción esto sería una tabla en la misma DB que la orden
    // Aquí usamos una cola en memoria para simplificar el ejemplo
    private readonly ConcurrentQueue<OutboxMessage> _queue = new();

    public void Enqueue(OutboxMessage message) =>
        _queue.Enqueue(message);

    public bool TryDequeue(out OutboxMessage? message) =>
        _queue.TryDequeue(out message);

    public int PendingCount => _queue.Count;
}
