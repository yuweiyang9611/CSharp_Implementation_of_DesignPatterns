using DesignPatterns.TeachingProjects.OnlineStore.Building;
using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Events;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;
using DesignPatterns.TeachingProjects.OnlineStore.Pricing;
using DesignPatterns.TeachingProjects.OnlineStore.Validation;

namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public sealed class CheckoutFacade(
    CheckoutValidationChain validationChain,
    PricingStrategySelector pricingStrategySelector,
    IOrderBuilder orderBuilder,
    PaymentProcessorCreator paymentProcessorCreator,
    IOrderEventPublisher eventPublisher,
    IOrderNumberGenerator orderNumberGenerator,
    ICheckoutTrace trace)
{
    public CheckoutResult Checkout(CheckoutCommand command)
    {
        trace.Add("[Facade] 开始结账：调用者只需提交一份 CheckoutCommand。");

        ValidationReport validation = validationChain.Validate(
            new CheckoutValidationContext(
                command.Cart,
                command.Customer,
                command.ShippingAddress,
                command.ShippingFee));

        if (!validation.IsValid)
        {
            string failure = validation.FailedStep?.Message ?? "结账校验失败。";
            trace.Add($"[Facade] 结账提前结束：{failure}");
            return new CheckoutResult(false, failure, validation, null, null);
        }

        IPricingStrategy pricingStrategy = pricingStrategySelector.Select(command.Customer);
        trace.Add($"[Strategy] 选择“{pricingStrategy.Name}”计算订单价格。");

        IReadOnlyList<OrderLine> lines = command.Cart.Items
            .Select(item => new OrderLine(
                item.Product.Sku,
                item.Product.Name,
                item.Product.UnitPrice,
                item.Quantity))
            .ToArray();

        PricingBreakdown pricing = pricingStrategy.Calculate(lines, command.ShippingFee);
        trace.Add(
            $"[Strategy] 商品 {pricing.Subtotal:C2} - 优惠 {pricing.Discount:C2} + 运费 {pricing.ShippingFee:C2} = 应付 {pricing.Total:C2}。");

        Order order = orderBuilder
            .Reset()
            .ForCustomer(command.Customer)
            .DeliverTo(command.ShippingAddress)
            .AddItemsFrom(command.Cart)
            .WithPricing(pricing)
            .Build(orderNumberGenerator.Next());

        eventPublisher.Publish(new OrderPlacedEvent(order));

        PaymentResult payment = paymentProcessorCreator.Pay(
            new PaymentRequest(order.Number, pricing.Total, command.PaymentMethod));

        if (!payment.IsApproved)
        {
            order.Cancel(payment.Message);
            trace.Add($"[Facade] 支付未通过，订单已取消：{payment.Message}");
            return new CheckoutResult(false, payment.Message, validation, order, payment);
        }

        order.Pay(payment.Reference);
        trace.Add($"[Facade] 结账完成，订单 {order.Number} 当前状态：{order.Status}。");
        return new CheckoutResult(true, "结账成功。", validation, order, payment);
    }
}
