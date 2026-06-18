using System.Collections.Concurrent;

namespace RealTimeInventorySystem.Services;

// Parte 4: Simulación de Azure Service Bus con Topic + Subscriptions + DLQ
//
// Equivalencia con Azure Service Bus real:
//   InMemoryServiceBus  ↔  Topic
//   Subscription        ↔  Subscription (cada consumidor tiene la suya)
//   DLQ                 ↔  $DeadLetterQueue (mensajes que fallaron maxDelivery veces)
//
// Por qué Topic y no Queue:
//   Queue → un solo consumidor recibe el mensaje (competencia)
//   Topic → TODOS los suscriptores reciben una copia independiente
//   Para OrderCreated con Inventory + Billing + Notifications → Topic es lo correcto
public class InMemoryServiceBus
{
    private readonly ILogger<InMemoryServiceBus> _logger;

    // Cada suscriptor tiene su propia cola — independientes entre sí
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ServiceBusMessage>> _subscriptions = new();

    // Dead Letter Queue: mensajes que fallaron el número máximo de entregas
    private readonly ConcurrentQueue<ServiceBusMessage> _deadLetterQueue = new();

    private const int MaxDeliveryCount = 3;

    public InMemoryServiceBus(ILogger<InMemoryServiceBus> logger) => _logger = logger;

    // Registrar un suscriptor (equivale a crear una Subscription en el Topic)
    public void Subscribe(string subscriberName)
    {
        _subscriptions.TryAdd(subscriberName, new ConcurrentQueue<ServiceBusMessage>());
        _logger.LogInformation("[ServiceBus] Suscriptor '{Subscriber}' registrado", subscriberName);
    }

    // Publicar un mensaje al topic — todos los suscriptores reciben una copia
    public void Publish(string eventType, string payload)
    {
        foreach (var (name, queue) in _subscriptions)
        {
            var msg = new ServiceBusMessage
            {
                MessageId  = Guid.NewGuid(),
                EventType  = eventType,
                Payload    = payload,
                EnqueuedAt = DateTime.UtcNow
            };

            queue.Enqueue(msg);
            _logger.LogInformation("[ServiceBus] Mensaje {MsgId} encolado para '{Subscriber}'", msg.MessageId, name);
        }
    }

    // El suscriptor consume su próximo mensaje
    // Si lo procesa OK → Complete (se elimina)
    // Si falla → DeadLetter cuando supera MaxDeliveryCount
    public ServiceBusMessage? Receive(string subscriberName)
    {
        if (!_subscriptions.TryGetValue(subscriberName, out var queue))
            return null;

        return queue.TryPeek(out var msg) ? msg : null;
    }

    public void Complete(string subscriberName, Guid messageId)
    {
        if (!_subscriptions.TryGetValue(subscriberName, out var queue))
            return;

        if (queue.TryDequeue(out var msg) && msg.MessageId == messageId)
            _logger.LogInformation("[ServiceBus] '{Subscriber}' completó mensaje {MsgId}", subscriberName, messageId);
    }

    // Falla la entrega — incrementa el contador; si supera el máximo → DLQ
    public void Abandon(string subscriberName, Guid messageId, string reason)
    {
        if (!_subscriptions.TryGetValue(subscriberName, out var queue))
            return;

        if (!queue.TryDequeue(out var msg) || msg.MessageId != messageId)
            return;

        msg.DeliveryCount++;
        msg.LastFailureReason = reason;

        if (msg.DeliveryCount >= MaxDeliveryCount)
        {
            _deadLetterQueue.Enqueue(msg);
            _logger.LogError(
                "[ServiceBus] Mensaje {MsgId} enviado a DLQ tras {Count} intentos. Razón: {Reason}",
                messageId, msg.DeliveryCount, reason);
        }
        else
        {
            queue.Enqueue(msg); // reencola para reintento
            _logger.LogWarning(
                "[ServiceBus] Mensaje {MsgId} abandonado ({Count}/{Max}). Razón: {Reason}",
                messageId, msg.DeliveryCount, MaxDeliveryCount, reason);
        }
    }

    public IReadOnlyList<ServiceBusMessage> GetPending(string subscriberName) =>
        _subscriptions.TryGetValue(subscriberName, out var q) ? q.ToList() : Array.Empty<ServiceBusMessage>();

    public IReadOnlyList<ServiceBusMessage> GetDeadLetterQueue() =>
        _deadLetterQueue.ToList();
}

public class ServiceBusMessage
{
    public Guid MessageId { get; init; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime EnqueuedAt { get; init; }
    public int DeliveryCount { get; set; }
    public string? LastFailureReason { get; set; }
}
