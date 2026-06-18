using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Data;

namespace RealTimeInventorySystem.Controllers;

// Parte 5: Concurrencia Optimista con EF Core
//
// PATRÓN COMPLETO (lo que EF Core genera en SQL Server):
//
//   Modelo:
//     [Timestamp]
//     public byte[] RowVersion { get; set; }      ← SQL Server lo maneja automáticamente
//
//   DbContext:
//     modelBuilder.Entity<Order>()
//         .Property(x => x.RowVersion)
//         .IsRowVersion();                         ← genera "rowversion" / "timestamp" en SQL
//
//   SQL generado por EF Core en el UPDATE:
//     UPDATE Orders
//     SET Status = @newStatus
//     WHERE Id = @id AND RowVersion = @originalRowVersion   ← chequeo atómico
//
//   Si otro usuario ya actualizó → WHERE no matchea → 0 rows affected → DbUpdateConcurrencyException
//
// Este proyecto ya demuestra el patrón en InventoryController + InventoryService
// (usando IsConcurrencyToken() + Version manual).
// El endpoint de abajo muestra el flujo GET → UPDATE → 409 de forma explícita.
//
// Diferencia IsConcurrencyToken() vs IsRowVersion():
//   IsConcurrencyToken() → vos incrementás Version manualmente
//   IsRowVersion()       → SQL Server actualiza RowVersion automáticamente (más seguro)
[ApiController]
[Route("api/ef-concurrency")]
public class EfConcurrencyController : ControllerBase
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<EfConcurrencyController> _logger;

    public EfConcurrencyController(InventoryDbContext db, ILogger<EfConcurrencyController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // Muestra el estado actual del Inventory + su Version (token de concurrencia)
    // El cliente debe enviar este Version en el PUT con el header If-Version
    [HttpGet("inventory/{inventoryId:int}")]
    public async Task<IActionResult> GetInventory(int inventoryId)
    {
        var inv = await _db.Inventories.FindAsync(inventoryId);

        if (inv is null)
            return NotFound(new { Message = $"Inventory {inventoryId} no encontrado." });

        return Ok(new
        {
            inv.InventoryId,
            inv.ProductId,
            inv.WarehouseId,
            inv.Quantity,
            inv.Version,
            Hint = "Enviá este Version en el header 'If-Version' del PUT para detectar conflictos."
        });
    }

    // Actualiza el Inventory solo si la Version coincide
    // Si otro proceso lo modificó antes → DbUpdateConcurrencyException → 409 Conflict
    //
    // En código con [Timestamp] / IsRowVersion() sería exactamente igual,
    // pero EF Core usaría el RowVersion generado por SQL en lugar de Version manual
    [HttpPut("inventory/{inventoryId:int}")]
    public async Task<IActionResult> UpdateInventory(
        int inventoryId,
        [FromHeader(Name = "If-Version")] long? ifVersion,
        [FromBody] UpdateInventoryRequest request)
    {
        if (ifVersion is null)
            return BadRequest(new { Message = "El header If-Version es requerido." });

        var inv = await _db.Inventories.FindAsync(inventoryId);

        if (inv is null)
            return NotFound();

        // Verificamos la versión antes de modificar
        // EF Core hace esto automáticamente en el WHERE del UPDATE
        if (inv.Version != ifVersion)
            return Conflict(new
            {
                Message       = "El recurso fue modificado por otro proceso.",
                YourVersion   = ifVersion,
                CurrentVersion = inv.Version,
                Hint          = "Hacé un GET para obtener la versión actual y reintentá."
            });

        inv.Quantity += request.QuantityDelta;
        inv.Version++;  // con IsRowVersion() esto lo haría SQL Server automáticamente

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // EF Core lanza esto si el UPDATE afectó 0 filas (otro proceso ganó la carrera)
            _logger.LogWarning("DbUpdateConcurrencyException en Inventory {Id}: {Msg}", inventoryId, ex.Message);

            return Conflict(new
            {
                Message = "409 Conflict — Lost Update detectado.",
                // 409 vs 412:
                //   409 Conflict  → cuando el cliente no incluyó ningún ETag/Version (conflicto de negocio)
                //   412 Precondition Failed → cuando el cliente envió ETag/Version y no coincidió
                //   Aquí usamos 409 porque aplica a la semántica de conflicto de recurso
                Hint = "EF Core detecta 0 filas afectadas → DbUpdateConcurrencyException → 409."
            });
        }

        return Ok(new { inv.InventoryId, inv.Quantity, inv.Version });
    }
}

public record UpdateInventoryRequest(int QuantityDelta);
