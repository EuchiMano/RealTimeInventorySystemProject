using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Services;

// Parte 5: Worker que procesa mensajes del Outbox en background
// En producción enviaría al broker/gateway con reintentos e idempotencia
public class OutboxProcessor : BackgroundService
{
    private readonly OutboxService _outbox;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        OutboxService outbox,
        ILogger<OutboxProcessor> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            while (_outbox.TryDequeue(out OutboxMessage? msg) && msg is not null)
            {
                // Aquí iría el envío real al payment gateway / message broker
                // Con reintentos y marcado como procesado en DB
                _logger.LogInformation(
                    "Processing outbox message [{Id}] Type={Type} Payload={Payload}",
                    msg.Id,
                    msg.Type,
                    msg.Payload);

                msg.Processed = true;
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}
