using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Pricing;

public sealed class PricingStrategySelector(
    IPricingStrategy standardStrategy,
    IPricingStrategy vipStrategy)
{
    public IPricingStrategy Select(Customer customer)
    {
        return customer.IsVip ? vipStrategy : standardStrategy;
    }
}
