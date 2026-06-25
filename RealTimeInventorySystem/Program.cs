using System.Threading.RateLimiting;
using RealTimeInventorySystem.Data;
using RealTimeInventorySystem.Services;
using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Options;
using RealTimeInventorySystem.Middleware;
using RealTimeInventorySystem.Services.Payments;
using RealTimeInventorySystem.Services.Idempotency;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<InventoryDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<InventoryService>();

builder.Services.AddSingleton<SupplierService>();

builder.Services.AddHttpClient();

// ── Parte 1: Zero-Allocation Parser ──────────────────────────────────────────
// OrderParserService es estático, no necesita registro en DI

// ── Parte 2: Idempotency ──────────────────────────────────────────────────────
// Singleton para que el store persista entre requests
builder.Services.AddSingleton<IdempotencyService>();

// ── Parte 3: Optimistic Concurrency ──────────────────────────────────────────
// Singleton para que el store en memoria sea compartido entre requests
builder.Services.AddSingleton<OrderConcurrencyService>();

// ── Parte 4: Options Pattern ──────────────────────────────────────────────────
builder.Services
    .AddOptions<PaymentGatewayOptions>()
    .Bind(builder.Configuration.GetSection(PaymentGatewayOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => Uri.IsWellFormedUriString(o.Endpoint, UriKind.Absolute),
        "Endpoint must be a valid absolute URI.")
    .ValidateOnStart();

builder.Services
    .AddOptions<FeatureFlagsOptions>()
    .Bind(builder.Configuration.GetSection(FeatureFlagsOptions.SectionName));

// PaymentGatewayService usa IOptionsMonitor → Singleton
builder.Services.AddSingleton<PaymentGatewayService>();
// FeatureFlagsService usa IOptionsSnapshot → Scoped
builder.Services.AddScoped<FeatureFlagsService>();

// ── Parte 5: Outbox Pattern ───────────────────────────────────────────────────
// OutboxService: Singleton porque es la cola compartida
builder.Services.AddSingleton<OutboxService>();
// OutboxProcessor: BackgroundService que procesa la cola
builder.Services.AddHostedService<OutboxProcessor>();

// ── Semana 3: Ejercicios Polly + Idempotencia ─────────────────────────────────

// Ejercicio 1: Gateway simulado con fallas aleatorias + Polly resilience pipeline
builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddSingleton<ResilientPaymentGatewayService>();

// Ejercicio 2: Consumidor idempotente con store de MessageIds procesados
builder.Services.AddSingleton<InMemoryProcessedMessageStore>();
builder.Services.AddSingleton<IProcessedMessageStore>(
    sp => sp.GetRequiredService<InMemoryProcessedMessageStore>());
builder.Services.AddSingleton<IdempotentInventoryProcessor>();

// ── Semana 2: Nuevos tópicos ──────────────────────────────────────────────────

// Ej. 2: Saga Pattern — orquestador in-memory con compensación
builder.Services.AddSingleton<CheckoutSagaService>();

// Ej. 3: Reintentos con Polly — FlakyGateway + ResilientPaymentService
builder.Services.AddSingleton<FlakyPaymentGatewayService>();
builder.Services.AddSingleton<ResilientPaymentService>();

// Ej. 4: Service Bus simulado — Topic + Subscriptions + DLQ
builder.Services.AddSingleton<InMemoryServiceBus>(sp =>
{
    var bus    = new InMemoryServiceBus(sp.GetRequiredService<ILogger<InMemoryServiceBus>>());
    // Pre-registrar los tres suscriptores del escenario del ejercicio
    bus.Subscribe("Inventory");
    bus.Subscribe("Billing");
    bus.Subscribe("Notifications");
    return bus;
});

// Ej. 6: Observabilidad — métricas con System.Diagnostics.Metrics
builder.Services.AddSingleton<AppMetrics>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("InventoryPolicy", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",

            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ej. 6: CorrelationId middleware — debe ir antes de los controllers
app.UseCorrelationId();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose the implicit Program class for integration tests (WebApplicationFactory)
public partial class Program { }