using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RealTimeInventorySystem.Options;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

// Parte 4: Options Pattern — IOptions vs IOptionsSnapshot vs IOptionsMonitor
// Endpoint: GET /api/payment-demo/...
[ApiController]
[Route("api/payment-demo")]
public class PaymentDemoController : ControllerBase
{
    private readonly PaymentGatewayService _gateway;
    private readonly FeatureFlagsService   _featureFlags;

    // Parte 4A: IOptions<T> inyectado directamente en el controller
    // Usamos IOptions cuando la config es estática y no necesita actualizarse en caliente
    private readonly PaymentGatewayOptions _staticOptions;

    public PaymentDemoController(
        PaymentGatewayService gateway,
        FeatureFlagsService featureFlags,
        IOptions<PaymentGatewayOptions> staticOptions)
    {
        _gateway       = gateway;
        _featureFlags  = featureFlags;
        _staticOptions = staticOptions.Value; // leído una sola vez al construir
    }

    // 4A: Devuelve la config estática leída con IOptions (valor fijo del arranque)
    [HttpGet("static-config")]
    public IActionResult GetStaticConfig()
    {
        return Ok(new
        {
            Source         = "IOptions — config leída una vez al arrancar",
            Endpoint       = _staticOptions.Endpoint,
            TimeoutSeconds = _staticOptions.TimeoutSeconds,
            // ApiKey oculto intencionalmente
        });
    }

    // 4B: IOptionsSnapshot — recalculado por request (scoped)
    // Cada request ve los valores más recientes de appsettings.json
    [HttpGet("feature-flags")]
    public IActionResult GetFeatureFlags()
    {
        return Ok(new
        {
            Source           = "IOptionsSnapshot — recalculado por request",
            DiscountsEnabled = _featureFlags.DiscountsEnabled(),
            LoyaltyEnabled   = _featureFlags.LoyaltyPointsEnabled()
        });
    }

    // 4C: IOptionsMonitor — singleton que reacciona a cambios en caliente
    // El servicio usa CurrentValue en cada llamada, nunca en el constructor
    [HttpPost("charge")]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { Message = "Amount must be positive." });

        var chargeId = await _gateway.ChargeAsync(request.Amount);

        return Ok(new
        {
            Source   = "IOptionsMonitor — config leída en cada uso (CurrentValue)",
            ChargeId = chargeId,
            Amount   = request.Amount
        });
    }
}

public record ChargeRequest(decimal Amount);
