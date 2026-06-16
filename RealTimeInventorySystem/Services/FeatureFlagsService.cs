using Microsoft.Extensions.Options;
using RealTimeInventorySystem.Options;

namespace RealTimeInventorySystem.Services;

// Parte 4B: IOptionsSnapshot — scoped, se recalcula por request
// Escenario: feature flags que pueden cambiar entre requests sin reiniciar la app
public class FeatureFlagsService
{
    private readonly FeatureFlagsOptions _flags;

    // IOptionsSnapshot es Scoped, por lo que cada request recibe valores frescos
    public FeatureFlagsService(IOptionsSnapshot<FeatureFlagsOptions> snapshot)
    {
        _flags = snapshot.Value;
    }

    public bool DiscountsEnabled()     => _flags.EnableDiscounts;
    public bool LoyaltyPointsEnabled() => _flags.EnableLoyaltyPoints;
}
