using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;

namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public sealed record DemoRun(
    CheckoutResult CheckoutResult,
    OnlineStoreSystem System,
    Product Book,
    Product Mug);

public static class DemoScenario
{
    public static DemoRun Run(bool echoTrace)
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace);
        ICheckoutTrace trace = system.Trace;

        trace.Add("=== 场景：VIP 客户购买设计模式书和 C# 马克杯 ===");

        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        Product mug = system.Catalog.GetRequired("MUG-CSHARP");
        ShoppingCart cart = new();
        cart.Add(book, quantity: 1);
        cart.Add(mug, quantity: 2);
        trace.Add("[Cart] 已加入《C# 设计模式实战》×1、C# 马克杯×2。");

        Customer customer = new("C-1001", "林晓", IsVip: true);
        ShippingAddress address = new("东京都", "千代田区", "丸之内 1-1", "100-0005");
        PaymentMethod paymentMethod = new("WALLET-88", AvailableBalance: 1_000m);

        CheckoutResult result = system.Checkout.Checkout(
            new CheckoutCommand(
                cart,
                customer,
                address,
                ShippingFee: 12m,
                paymentMethod));

        if (!result.IsSuccess || result.Order is null)
        {
            throw new InvalidOperationException($"演示订单结账失败：{result.Message}");
        }

        Order order = result.Order;
        trace.Add("[Fulfillment] 仓库把包裹交给承运商。");
        order.Ship("SF-TEST-0001");
        trace.Add("[Fulfillment] 客户确认收货。");
        order.Complete();

        trace.Add(
            $"=== 完成：{order.Number}，状态 {order.Status}，应付 {order.Pricing.Total:C2}，支付流水 {order.PaymentReference} ===");

        return new DemoRun(result, system, book, mug);
    }
}
