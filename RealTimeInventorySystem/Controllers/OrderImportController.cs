using Microsoft.AspNetCore.Mvc;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 1: Zero-allocation parsing con ReadOnlySpan<char>
// Endpoint: POST /api/order-import
[ApiController]
[Route("api/order-import")]
public class OrderImportController : ControllerBase
{
    // Acepta un body con líneas de texto separadas por newline
    // Formato por línea: Timestamp|OrderId|UserId|Amount
    // Ejemplo:
    //   2026-06-08T12:30:15Z|ORD-1001|USER-99|250.50
    //   2026-06-08T12:30:20Z|ORD-1002|USER-55|100.00
    [HttpPost]
    public IActionResult Import([FromBody] string[] lines)
    {
        if (lines is null || lines.Length == 0)
            return BadRequest(new { Message = "No lines provided." });

        var records = OrderParserService.ParseLines(lines);

        return Ok(new
        {
            Parsed   = records.Count,
            // Span<char> no puede cruzar await ni ser devuelto al cliente,
            // por eso el record tiene strings — inevitables en la boundary de la API
            Records  = records
        });
    }

    // Endpoint de demostración de una sola línea para ver el parse rápido
    [HttpPost("single")]
    public IActionResult ParseSingle([FromBody] string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return BadRequest(new { Message = "Line is empty." });

        var record = OrderParserService.Parse(line);

        return Ok(record);
    }
}
