using CheckoutRefactoringKata.Contracts;

namespace CheckoutRefactoringKata.Reference.Pricing;

public interface IPricingStrategy
{
    PricingPlan Plan { get; }

    PricingBreakdown Calculate(CheckoutRequest request);
}

public sealed class StandardPricingStrategy : IPricingStrategy
{
    public PricingPlan Plan => PricingPlan.Standard;

    public PricingBreakdown Calculate(CheckoutRequest request) =>
        PricingMath.Calculate(request, discountRate: 0m);
}

public sealed class MemberPricingStrategy : IPricingStrategy
{
    public PricingPlan Plan => PricingPlan.Member;

    public PricingBreakdown Calculate(CheckoutRequest request) =>
        PricingMath.Calculate(request, discountRate: 0.10m);
}

public sealed class FlashSalePricingStrategy : IPricingStrategy
{
    public PricingPlan Plan => PricingPlan.FlashSale;

    public PricingBreakdown Calculate(CheckoutRequest request) =>
        PricingMath.Calculate(request, discountRate: 0.20m);
}

internal static class PricingMath
{
    public static PricingBreakdown Calculate(
        CheckoutRequest request,
        decimal discountRate)
    {
        var subtotal = decimal.Round(
            request.Items.Sum(item => item.UnitPrice * item.Quantity),
            2,
            MidpointRounding.AwayFromZero);
        var discount = decimal.Round(
            subtotal * discountRate,
            2,
            MidpointRounding.AwayFromZero);
        var discountedMerchandise = subtotal - discount;
        var shippingFee = discountedMerchandise >= 100m
            ? 0m
            : string.Equals(request.ShippingCountry, "CN", StringComparison.OrdinalIgnoreCase)
                ? 12m
                : 30m;

        return new PricingBreakdown(
            subtotal,
            discount,
            shippingFee,
            discountedMerchandise + shippingFee);
    }
}
