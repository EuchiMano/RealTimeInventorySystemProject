namespace RealTimeInventorySystem.Services.Payments;

// Interfaz que abstrae el gateway externo de pagos
// Permite testear la lógica de resiliencia sin depender del servicio real
public interface IPaymentGateway
{
    // Lanza TimeoutException     → 30% de probabilidad (error transitorio → reintentar)
    // Lanza HttpRequestException → 20% de probabilidad (error transitorio → reintentar)
    // Retorna chargeId           → 50% de probabilidad (éxito)
    Task<string> ProcessPaymentAsync(string orderId, decimal amount);
}
