using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Services;

// Parte 1: Zero-allocation parser usando ReadOnlySpan<char>
// Demuestra el uso de Slice + IndexOf sin Split/Substring/Regex
public static class OrderParserService
{
    // Formato esperado: Timestamp|OrderId|UserId|Amount
    // Ejemplo: 2026-06-08T12:30:15Z|ORD-1001|USER-99|250.50
    public static OrderImportRecord Parse(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan();

        // Buscar posición de cada separador '|'
        int p1 = span.IndexOf('|');

        // Para el segundo '|', buscamos en el segmento que sigue al primero
        int p2 = span[(p1 + 1)..].IndexOf('|') + p1 + 1;

        int p3 = span[(p2 + 1)..].IndexOf('|') + p2 + 1;

        // Extraer cada campo como slice — sin crear strings intermedios
        ReadOnlySpan<char> timestampSpan = span[..p1];
        ReadOnlySpan<char> orderIdSpan   = span[(p1 + 1)..p2];
        ReadOnlySpan<char> userIdSpan    = span[(p2 + 1)..p3];
        ReadOnlySpan<char> amountSpan    = span[(p3 + 1)..];

        // Allocations inevitables: DateTime.Parse y decimal.Parse aceptan Span<char>
        // ToString() en OrderId y UserId es inevitable porque el record los guarda como string
        DateTime timestamp = DateTime.Parse(timestampSpan);
        decimal  amount    = decimal.Parse(amountSpan);

        return new OrderImportRecord(
            timestamp,
            orderIdSpan.ToString(),
            userIdSpan.ToString(),
            amount);
    }

    // Parsea múltiples líneas y devuelve los registros
    public static IReadOnlyList<OrderImportRecord> ParseLines(
        IEnumerable<string> lines)
    {
        var results = new List<OrderImportRecord>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            results.Add(Parse(line));
        }

        return results;
    }
}
