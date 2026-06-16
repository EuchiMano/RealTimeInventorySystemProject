using System.Collections.Concurrent;

namespace RealTimeInventorySystem.Services;

// Parte 3: Concurrencia optimista con ETag + If-Match
// Simula un store en memoria para mantener el ejemplo simple y sin DB
public class OrderConcurrencyService
{
    private readonly ConcurrentDictionary<string, OrderRecord> _orders = new();

    public OrderConcurrencyService()
    {
        // Seed con una orden de ejemplo
        _orders["ORD-1001"] = new OrderRecord("ORD-1001", "Pending", GenerateETag("Pending", 1), 1);
    }

    public OrderRecord? Get(string orderId) =>
        _orders.TryGetValue(orderId, out var order) ? order : null;

    // Retorna false si el ETag no coincide (Lost Update detectado)
    public bool TryUpdate(
        string orderId,
        string newStatus,
        string ifMatchETag,
        out OrderRecord? updated)
    {
        updated = null;

        if (!_orders.TryGetValue(orderId, out var current))
            return false;

        // ETag no coincide → otro usuario ya modificó el recurso
        if (current.ETag != ifMatchETag)
            return false;

        var next = current with
        {
            Status  = newStatus,
            Version = current.Version + 1,
            ETag    = GenerateETag(newStatus, current.Version + 1)
        };

        // Intento atómico: solo reemplaza si el valor sigue siendo el mismo
        if (!_orders.TryUpdate(orderId, next, current))
            return false;

        updated = next;
        return true;
    }

    private static string GenerateETag(string status, int version) =>
        Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{status}-{version}"));
}

public record OrderRecord(
    string OrderId,
    string Status,
    string ETag,
    int    Version);
