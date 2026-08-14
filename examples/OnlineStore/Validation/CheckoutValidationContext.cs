using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Validation;

public sealed record CheckoutValidationContext(
    ShoppingCart Cart,
    Customer Customer,
    ShippingAddress ShippingAddress,
    decimal ShippingFee);

public sealed record ValidationStep(string Rule, bool Passed, string Message);

public sealed record ValidationReport(IReadOnlyList<ValidationStep> Steps)
{
    public bool IsValid => Steps.All(step => step.Passed);

    public ValidationStep? FailedStep => Steps.FirstOrDefault(step => !step.Passed);
}
