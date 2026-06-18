namespace RealTimeInventorySystem.Middleware;

// Parte 6: Middleware de CorrelationId
// Lee el header X-Correlation-Id (o genera uno nuevo si no viene)
// Lo guarda en HttpContext.Items y lo agrega a la respuesta
// Permite trazar un request a través de logs distribuidos
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Si el cliente envía su propio CorrelationId, lo reutilizamos
        // Esto permite trazar requests que vienen de otro microservicio
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = correlationId;

        // Agregar a la respuesta para que el cliente pueda correlacionar
        context.Response.Headers[HeaderName] = correlationId;

        // Agregar al scope de logging — todos los logs de este request incluirán el CorrelationId
        using (context.RequestServices
            .GetRequiredService<ILogger<CorrelationIdMiddleware>>()
            .BeginScope(new Dictionary<string, object> { [HeaderName] = correlationId }))
        {
            await _next(context);
        }
    }
}

// Extension method para registrar el middleware de forma limpia en Program.cs
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
