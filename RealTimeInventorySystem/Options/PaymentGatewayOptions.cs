using System.ComponentModel.DataAnnotations;

namespace RealTimeInventorySystem.Options;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    [Required]
    public string Endpoint { get; init; } = string.Empty;

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; }
}
