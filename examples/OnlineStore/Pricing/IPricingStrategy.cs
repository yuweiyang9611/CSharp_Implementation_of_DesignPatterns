using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Pricing;

public interface IPricingStrategy
{
    string Name { get; }

    PricingBreakdown Calculate(IReadOnlyList<OrderLine> lines, decimal shippingFee);
}
