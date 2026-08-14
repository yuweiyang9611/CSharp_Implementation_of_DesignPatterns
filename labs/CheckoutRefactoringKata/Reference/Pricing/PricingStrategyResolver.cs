using CheckoutRefactoringKata.Contracts;

namespace CheckoutRefactoringKata.Reference.Pricing;

public sealed class PricingStrategyResolver
{
    private readonly IReadOnlyDictionary<PricingPlan, IPricingStrategy> _strategies;

    public PricingStrategyResolver(IEnumerable<IPricingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _strategies = strategies.ToDictionary(strategy => strategy.Plan);
    }

    public IPricingStrategy Resolve(PricingPlan plan) =>
        _strategies.TryGetValue(plan, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"没有注册 {plan} 价格策略。");

    public static PricingStrategyResolver CreateDefault() =>
        new(
        [
            new StandardPricingStrategy(),
            new MemberPricingStrategy(),
            new FlashSalePricingStrategy(),
        ]);
}
