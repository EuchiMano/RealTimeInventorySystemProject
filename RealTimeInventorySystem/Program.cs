using System.Threading.RateLimiting;
using RealTimeInventorySystem.Data;
using RealTimeInventorySystem.Services;
using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Options;

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

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose the implicit Program class for integration tests (WebApplicationFactory)
public partial class Program { }