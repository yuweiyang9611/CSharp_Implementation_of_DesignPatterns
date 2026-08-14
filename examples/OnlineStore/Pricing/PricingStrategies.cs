using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Pricing;

public sealed class StandardPricingStrategy : IPricingStrategy
{
    public string Name => "标准定价";

    public PricingBreakdown Calculate(IReadOnlyList<OrderLine> lines, decimal shippingFee)
    {
        decimal subtotal = lines.Sum(line => line.LineTotal);
        return new PricingBreakdown(subtotal, 0m, shippingFee, subtotal + shippingFee, Name);
    }
}

public sealed class VipPricingStrategy : IPricingStrategy
{
    private readonly decimal _discountRate;

    public VipPricingStrategy(decimal discountRate)
    {
        if (discountRate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(discountRate), "折扣率必须位于 0 到 1 之间。");
        }

        _discountRate = discountRate;
    }

    public string Name => $"VIP {_discountRate:P0} 折扣";

    public PricingBreakdown Calculate(IReadOnlyList<OrderLine> lines, decimal shippingFee)
    {
        decimal subtotal = lines.Sum(line => line.LineTotal);
        decimal discount = decimal.Round(subtotal * _discountRate, 2, MidpointRounding.AwayFromZero);
        return new PricingBreakdown(subtotal, discount, shippingFee, subtotal - discount + shippingFee, Name);
    }
}
