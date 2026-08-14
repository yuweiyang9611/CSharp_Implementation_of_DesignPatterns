using CheckoutRefactoringKata.Contracts;
using CheckoutRefactoringKata.Reference.Application;

namespace CheckoutRefactoringKata.Reference;

public static class Program
{
    public static int Main(string[] args)
    {
        var scenario = args.FirstOrDefault()?.ToLowerInvariant() ?? "success";
        var request = CreateRequest(scenario);
        var facade = CheckoutFacade.CreateDefault(
            new DemoPaymentGateway(),
            new ConsoleReceiptStore());

        var result = facade.Checkout(request);
        PrintResult("Reference：模式协作后的结账门面", result);
        return 0;
    }

    private static CheckoutRequest CreateRequest(string scenario)
    {
        var items = scenario == "out-of-stock"
            ? new[] { new CartItem("BOOK-DP", 80m, 2, 1) }
            : [new CartItem("BOOK-DP", 80m, 1, 10), new CartItem("CARD", 20m, 2, 10)];

        return new CheckoutRequest(
            "ORDER-1001",
            PricingPlan.Member,
            items,
            "CN",
            scenario == "decline" ? "DECLINE-DEMO" : "PAY-DEMO",
            TermsAccepted: true);
    }

    private static void PrintResult(string title, CheckoutResult result)
    {
        Console.WriteLine($"=== {title} ===");

        if (result.IsSuccess)
        {
            var receipt = result.Receipt!;
            Console.WriteLine($"订单：{receipt.OrderId}");
            Console.WriteLine($"小计：{receipt.Pricing.Subtotal:C}");
            Console.WriteLine($"优惠：{receipt.Pricing.Discount:C}");
            Console.WriteLine($"运费：{receipt.Pricing.ShippingFee:C}");
            Console.WriteLine($"实付：{receipt.Pricing.Total:C}");
            Console.WriteLine($"状态：{receipt.Status}");
        }
        else
        {
            Console.WriteLine($"失败：{result.Failure!.Code} - {result.Failure.Message}");
        }

        Console.WriteLine("轨迹：");
        foreach (var step in result.Trace)
        {
            Console.WriteLine($"  -> {step}");
        }
    }
}

internal sealed class DemoPaymentGateway : IPaymentGateway
{
    public PaymentDecision Charge(string orderId, decimal amount, string paymentToken) =>
        paymentToken.StartsWith("DECLINE", StringComparison.OrdinalIgnoreCase)
            ? PaymentDecision.Decline("演示网关拒绝了支付。")
            : PaymentDecision.Approve($"TX-{orderId}-{amount:0.00}");
}

internal sealed class ConsoleReceiptStore : IReceiptStore
{
    public void Save(CheckoutReceipt receipt) =>
        Console.WriteLine($"[收据存储] 已保存 {receipt.OrderId} / {receipt.TransactionId}");
}
