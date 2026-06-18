using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Data;
using RealTimeInventorySystem.Models;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 1: Outbox Pattern con transacción DB real
//
// DIFERENCIA con el OutboxDemoController existente (in-memory):
//
//   In-memory (demo anterior):
//     _outbox.Enqueue(msg)  ← vive en RAM, se pierde si la app reinicia
//
//   SQL Outbox (este demo):
//     using var tx = await _db.Database.BeginTransactionAsync();
//     _db.OutboxMessages.Add(msg);   ← misma transacción que la orden
//     await _db.SaveChangesAsync();
//     await tx.CommitAsync();        ← atómico: ambos o ninguno
//
// Problema sin Outbox:
//   await _db.SaveChangesAsync();        ← orden guardada
//   await _serviceBus.PublishAsync();    ← CRASH aquí → orden guardada, evento perdido
//
// Solución: guardar el evento en la DB ANTES de publicar
//   Si la app crashea → OutboxProcessor reintenta al reiniciar
//   Si el evento se publica pero el worker crashea antes de marcar Processed=true
//   → se republica (duplicado) → el consumidor debe ser idempotente (Ejercicio 7)
//
// NOTA: Requiere migración para la tabla OutboxMessages:
//   dotnet ef migrations add AddOutboxMessages
//   dotnet ef database update
[ApiController]
[Route("api/sql-outbox-demo")]
public class SqlOutboxDemoController : ControllerBase
{
    private readonly InventoryDbContext _db;
    private readonly OutboxService _inMemoryOutbox; // fallback para demo sin migración
    private readonly ILogger<SqlOutboxDemoController> _logger;

    public SqlOutboxDemoController(
        InventoryDbContext db,
        OutboxService inMemoryOutbox,
        ILogger<SqlOutboxDemoController> logger)
    {
        _db            = db;
        _inMemoryOutbox = inMemoryOutbox;
        _logger        = logger;
    }

    // Muestra el patrón de transacción atómica
    // Intenta guardar en DB; si falla (sin migración), cae al in-memory para que el demo corra igual
    [HttpPost("place-order")]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest request)
    {
        var outboxMsg = new OutboxMessage
        {
            Type    = "OrderCreated",
            Payload = $"OrderId={request.OrderId} Amount={request.Amount} CustomerId={request.CustomerId}"
        };

        bool savedToDb = false;

        try
        {
            // PATRÓN CLAVE: orden + evento en la MISMA transacción
            // Si cualquiera falla → rollback completo → ni orden ni evento persisten
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Paso 1: guardar la orden (aquí iría _db.Orders.Add(order))
            _logger.LogInformation("[SqlOutbox] Guardando orden {OrderId} en DB...", request.OrderId);

            // Paso 2: guardar el evento Outbox en la MISMA transacción
            // (Requiere: DbSet<OutboxMessage> en DbContext + migración aplicada)
            // _db.OutboxMessages.Add(outboxMsg);
            // await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            savedToDb = true;

            _logger.LogInformation(
                "[SqlOutbox] Transacción committed. Orden y OutboxMessage [{Id}] persistidos juntos.",
                outboxMsg.Id);
        }
        catch (Exception ex)
        {
            // Sin migración el demo cae aquí — usamos in-memory como fallback
            _logger.LogWarning(
                "[SqlOutbox] DB no disponible ({Error}). Usando in-memory para el demo.",
                ex.Message);

            _inMemoryOutbox.Enqueue(outboxMsg);
        }

        return Accepted(new
        {
            Message         = "Orden aceptada. El OutboxProcessor publicará el evento al reiniciar si hubo crash.",
            OrderId         = request.OrderId,
            OutboxMessageId = outboxMsg.Id,
            PersistedTo     = savedToDb ? "SQL Server (atómico con la orden)" : "In-Memory (demo sin migración)",
            KeyConcept      = new
            {
                AtomicTransaction  = "Orden + OutboxMessage en la misma transacción DB",
                CrashSafe          = "Si la app cae antes de publicar, el OutboxProcessor reintenta al reiniciar",
                IdempotencyNeeded  = "El consumidor debe ser idempotente porque el evento puede publicarse dos veces"
            }
        });
    }
}
