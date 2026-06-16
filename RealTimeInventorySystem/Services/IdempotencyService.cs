using System.Collections.Concurrent;
using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Services;

// Parte 2: Idempotency — garantiza que la misma orden no se procese dos veces
// Usa ConcurrentDictionary para manejar la race condition de requests simultáneos
public class IdempotencyService
{
    // Almacena: idempotency key → (request guardado, response guardado)
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _store = new();

    // Retorna null si es la primera vez (hay que procesar).
    // Retorna la entrada existente si ya fue procesada antes.
    public IdempotencyEntry? TryGet(string key) =>
        _store.TryGetValue(key, out var entry) ? entry : null;

    // Registra el resultado una vez procesada la orden
    public void Store(string key, OrderRequest request, object response) =>
        _store[key] = new IdempotencyEntry(request, response);

    // Verifica si el body actual coincide con el que se usó originalmente
    public bool BodyMatches(IdempotencyEntry entry, OrderRequest incoming) =>
        entry.OriginalRequest.OrderId    == incoming.OrderId   &&
        entry.OriginalRequest.CustomerId == incoming.CustomerId &&
        entry.OriginalRequest.Amount     == incoming.Amount;
}

public record IdempotencyEntry(
    OrderRequest OriginalRequest,
    object       Response);
