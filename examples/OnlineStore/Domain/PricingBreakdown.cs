namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed record PricingBreakdown(
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal Total,
    string AppliedStrategy);
