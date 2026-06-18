using System.Collections.Concurrent;
using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Services;

// Parte 2: Saga Pattern — orquestador de pasos con compensación
// Escenario: Crear Orden → Reservar Inventario → Cobrar Tarjeta → Enviar Confirmación
// Si cualquier paso falla, se deshacen los pasos completados en orden inverso
public class CheckoutSagaService
{
    private readonly ConcurrentDictionary<Guid, CheckoutSaga> _sagas = new();
    private readonly ILogger<CheckoutSagaService> _logger;

    public CheckoutSagaService(ILogger<CheckoutSagaService> logger) => _logger = logger;

    public CheckoutSaga? Get(Guid sagaId) =>
        _sagas.TryGetValue(sagaId, out var saga) ? saga : null;

    public async Task<CheckoutSaga> RunAsync(string orderId, int customerId, decimal amount)
    {
        var saga = new CheckoutSaga
        {
            OrderId    = orderId,
            CustomerId = customerId,
            Amount     = amount
        };

        _sagas[saga.SagaId] = saga;
        _logger.LogInformation("Saga [{SagaId}] iniciada para orden {OrderId}", saga.SagaId, orderId);

        // ── Paso 1: Crear Orden ────────────────────────────────────────────────
        await StepAsync(saga, "CreateOrder", () =>
        {
            _logger.LogInformation("[Saga] Orden {OrderId} guardada en DB", orderId);
        });

        // ── Paso 2: Reservar Inventario ───────────────────────────────────────
        await StepAsync(saga, "ReserveInventory", () =>
        {
            _logger.LogInformation("[Saga] Inventario reservado para orden {OrderId}", orderId);
        });

        if (saga.Status == SagaStatus.Pending)
            saga.Status = SagaStatus.InventoryReserved;

        // ── Paso 3: Cobrar Tarjeta (simulamos fallo si amount > 1000) ─────────
        await StepAsync(saga, "ChargePayment", () =>
        {
            if (amount > 1000)
                throw new InvalidOperationException("Tarjeta rechazada: límite excedido.");

            _logger.LogInformation("[Saga] Pago de {Amount} procesado", amount);
        });

        // ── Si el cobro falló, compensamos los pasos anteriores ───────────────
        if (saga.Status == SagaStatus.PaymentFailed)
        {
            await CompensateAsync(saga);
            return saga;
        }

        // ── Paso 4: Enviar Confirmación ───────────────────────────────────────
        await StepAsync(saga, "SendConfirmation", () =>
        {
            _logger.LogInformation("[Saga] Email de confirmación enviado al cliente {CustomerId}", customerId);
        });

        saga.Status = SagaStatus.Completed;
        _logger.LogInformation("Saga [{SagaId}] completada exitosamente", saga.SagaId);
        return saga;
    }

    private async Task StepAsync(CheckoutSaga saga, string stepName, Action step)
    {
        if (saga.Status is SagaStatus.PaymentFailed or SagaStatus.Compensated)
            return;

        try
        {
            await Task.Delay(20); // simula latencia de red / DB
            step();
            saga.CompletedSteps.Add(stepName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[Saga] Paso {Step} falló: {Error}", stepName, ex.Message);
            saga.Status = SagaStatus.PaymentFailed;
            saga.FailureReason = ex.Message;
        }
    }

    // Compensación: deshace en orden inverso los pasos completados
    // Cada compensación es la acción contraria al paso original
    private async Task CompensateAsync(CheckoutSaga saga)
    {
        _logger.LogWarning("Saga [{SagaId}] iniciando compensación...", saga.SagaId);

        foreach (var step in Enumerable.Reverse(saga.CompletedSteps))
        {
            await Task.Delay(20);

            var compensation = step switch
            {
                "ReserveInventory" => "ReleaseInventory",
                "CreateOrder"      => "CancelOrder",
                _                  => $"Undo_{step}"
            };

            _logger.LogInformation("[Saga] Compensando: {Compensation}", compensation);
            saga.CompensatedSteps.Add(compensation);
        }

        saga.Status = SagaStatus.Compensated;
        _logger.LogWarning("Saga [{SagaId}] compensada. Razón: {Reason}", saga.SagaId, saga.FailureReason);
    }
}
