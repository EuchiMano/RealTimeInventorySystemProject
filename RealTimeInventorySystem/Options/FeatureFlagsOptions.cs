namespace RealTimeInventorySystem.Options;

public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    public bool EnableDiscounts { get; init; }
    public bool EnableLoyaltyPoints { get; init; }
}
