using DesignPatterns.TeachingProjects.OnlineStore.Application;
using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Validation;

public sealed class CheckoutValidationChain(CheckoutValidationRule firstRule)
{
    public ValidationReport Validate(CheckoutValidationContext context)
    {
        List<ValidationStep> steps = [];
        firstRule.Execute(context, steps);
        return new ValidationReport(steps);
    }

    public static CheckoutValidationChain CreateDefault(ProductCatalog catalog, ICheckoutTrace trace)
    {
        CheckoutValidationRule first = new NonEmptyCartRule(trace);
        first
            .SetNext(new CustomerRule(trace))
            .SetNext(new ShippingAddressRule(trace))
            .SetNext(new ShippingFeeRule(trace))
            .SetNext(new StockAvailabilityRule(catalog, trace));
        return new CheckoutValidationChain(first);
    }
}

public sealed class NonEmptyCartRule(ICheckoutTrace trace) : CheckoutValidationRule(trace)
{
    protected override ValidationStep Evaluate(CheckoutValidationContext context)
    {
        bool passed = context.Cart.Items.Count > 0;
        return new ValidationStep(
            "购物车非空",
            passed,
            passed ? $"共有 {context.Cart.Items.Count} 种商品。" : "购物车为空。");
    }
}

public sealed class CustomerRule(ICheckoutTrace trace) : CheckoutValidationRule(trace)
{
    protected override ValidationStep Evaluate(CheckoutValidationContext context)
    {
        bool passed = !string.IsNullOrWhiteSpace(context.Customer.Id) &&
                      !string.IsNullOrWhiteSpace(context.Customer.Name);
        return new ValidationStep("客户信息", passed, passed ? "客户身份有效。" : "客户编号或姓名缺失。");
    }
}

public sealed class ShippingAddressRule(ICheckoutTrace trace) : CheckoutValidationRule(trace)
{
    protected override ValidationStep Evaluate(CheckoutValidationContext context)
    {
        bool passed = context.ShippingAddress.IsComplete;
        return new ValidationStep("收货地址", passed, passed ? "地址字段完整。" : "地址字段不完整。");
    }
}

public sealed class StockAvailabilityRule(
    ProductCatalog catalog,
    ICheckoutTrace trace) : CheckoutValidationRule(trace)
{
    protected override ValidationStep Evaluate(CheckoutValidationContext context)
    {
        CartItem? unavailable = context.Cart.Items.FirstOrDefault(
            item => !catalog.HasStock(item.Product.Sku, item.Quantity));
        bool passed = unavailable is null;
        return new ValidationStep(
            "库存可用",
            passed,
            passed ? "所有商品库存充足。" : $"{unavailable!.Product.Name} 库存不足。");
    }
}

public sealed class ShippingFeeRule(ICheckoutTrace trace) : CheckoutValidationRule(trace)
{
    protected override ValidationStep Evaluate(CheckoutValidationContext context)
    {
        bool passed = context.ShippingFee >= 0m;
        return new ValidationStep(
            "配送费用",
            passed,
            passed ? $"配送费用为 {context.ShippingFee:C2}。" : "配送费用不能为负数。");
    }
}
