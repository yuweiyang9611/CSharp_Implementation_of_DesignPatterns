using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;

namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public static class SelfTestRunner
{
    public static int Run(TextWriter output)
    {
        List<string> failures = [];

        RunCase("成功订单贯穿七种模式", VerifySuccessfulOrder, failures, output);
        RunCase("校验链在空购物车处短路", VerifyValidationStopsCheckout, failures, output);
        RunCase("同 SKU 数量先汇总再校验库存", VerifyDuplicateSkuCannotOverbook, failures, output);
        RunCase("负配送费用在计价和支付前被拒绝", VerifyNegativeShippingFeeRejected, failures, output);
        RunCase("支付失败触发取消状态与观察者", VerifyPaymentFailure, failures, output);

        if (failures.Count == 0)
        {
            output.WriteLine("SELF-TEST PASS: 5/5");
            return 0;
        }

        output.WriteLine($"SELF-TEST FAIL: {failures.Count} 个场景失败");
        foreach (string failure in failures)
        {
            output.WriteLine($"  - {failure}");
        }

        return 1;
    }

    private static void VerifySuccessfulOrder()
    {
        DemoRun run = DemoScenario.Run(echoTrace: false);
        Order order = Require(run.CheckoutResult.Order, "成功结账必须返回订单");

        Assert(run.CheckoutResult.IsSuccess, "结账结果应成功");
        Assert(order.Status == OrderStatus.Completed, "订单最终应为 Completed");
        Assert(order.Pricing.Subtotal == 286m, "商品小计应为 286.00");
        Assert(order.Pricing.Discount == 28.60m, "VIP 优惠应为 28.60");
        Assert(order.Pricing.Total == 269.40m, "订单总额应为 269.40");
        Assert(run.Book.AvailableStock == 7, "书籍库存应从 8 减为 7");
        Assert(run.Mug.AvailableStock == 10, "马克杯库存应从 12 减为 10");
        Assert(run.System.Notifications.Messages.Count == 4, "客户应收到下单、支付、发货、完成四条通知");
        Assert(
            run.System.Trace.Entries.Any(entry => entry.Contains("[Factory Method]", StringComparison.Ordinal)),
            "执行轨迹应包含 Factory Method");
        Assert(
            run.System.Trace.Entries.Any(entry => entry.Contains("[Chain]", StringComparison.Ordinal)),
            "执行轨迹应包含校验链");
    }

    private static void VerifyValidationStopsCheckout()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        CheckoutResult result = system.Checkout.Checkout(
            new CheckoutCommand(
                new ShoppingCart(),
                new Customer("C-EMPTY", "测试客户", IsVip: false),
                new ShippingAddress("东京都", "新宿区", "西新宿 1-1", "160-0023"),
                ShippingFee: 10m,
                new PaymentMethod("WALLET-EMPTY", AvailableBalance: 100m)));

        Assert(!result.IsSuccess, "空购物车应被拒绝");
        Assert(result.Order is null, "校验失败前不应创建订单");
        Assert(result.Payment is null, "校验失败前不应发起支付");
        Assert(result.Validation.Steps.Count == 1, "校验链应在首个失败规则处短路");
    }

    private static void VerifyPaymentFailure()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        ShoppingCart cart = new();
        cart.Add(book, 1);

        CheckoutResult result = system.Checkout.Checkout(
            new CheckoutCommand(
                cart,
                new Customer("C-NO-MONEY", "余额不足客户", IsVip: false),
                new ShippingAddress("大阪府", "大阪市", "梅田 1-1", "530-0001"),
                ShippingFee: 12m,
                new PaymentMethod("WALLET-ZERO", AvailableBalance: 0m)));

        Order order = Require(result.Order, "支付失败时应保留订单以便审计");
        Assert(!result.IsSuccess, "余额不足时结账应失败");
        Assert(order.Status == OrderStatus.Cancelled, "支付失败后订单应取消");
        Assert(book.AvailableStock == 8, "未支付订单不得扣减库存");
        Assert(system.Notifications.Messages.Count == 2, "客户应收到下单与取消通知");

        bool invalidTransitionRejected = false;
        try
        {
            order.Ship("SHOULD-NOT-SHIP");
        }
        catch (InvalidOperationException)
        {
            invalidTransitionRejected = true;
        }

        Assert(invalidTransitionRejected, "Cancelled 状态必须拒绝发货操作");
    }

    private static void VerifyDuplicateSkuCannotOverbook()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        ShoppingCart cart = new();
        cart.Add(book, 5);
        cart.Add(book, 4);

        CheckoutResult result = system.Checkout.Checkout(
            new CheckoutCommand(
                cart,
                new Customer("C-DUPLICATE", "重复商品客户", IsVip: false),
                new ShippingAddress("东京都", "千代田区", "丸之内 1-1", "100-0005"),
                ShippingFee: 12m,
                new PaymentMethod("WALLET-DUPLICATE", AvailableBalance: 5000m)));

        Assert(cart.Items.Count == 1, "同 SKU 应合并成一个购物车条目");
        Assert(cart.Items[0].Quantity == 9, "合并后的数量应为 9");
        Assert(!result.IsSuccess, "库存只有 8 件时，总数量 9 应被拒绝");
        Assert(result.Order is null, "库存校验失败前不应创建订单");
        Assert(result.Payment is null, "库存校验失败前不应发起支付");
        Assert(book.AvailableStock == 8, "失败后库存必须保持原值，不能部分扣减");
    }

    private static void VerifyNegativeShippingFeeRejected()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        ShoppingCart cart = new();
        cart.Add(book, 1);

        CheckoutResult result = system.Checkout.Checkout(
            new CheckoutCommand(
                cart,
                new Customer("C-BAD-FEE", "异常运费客户", IsVip: false),
                new ShippingAddress("东京都", "港区", "芝公园 4-2", "105-0011"),
                ShippingFee: -500m,
                new PaymentMethod("WALLET-BAD-FEE", AvailableBalance: 1000m)));

        Assert(!result.IsSuccess, "负配送费用必须被拒绝");
        Assert(result.Validation.FailedStep?.Rule == "配送费用", "应由配送费用规则拒绝请求");
        Assert(result.Order is null, "金额校验失败前不应创建订单");
        Assert(result.Payment is null, "金额校验失败前不应发起支付");
        Assert(book.AvailableStock == 8, "金额校验失败不得修改库存");
    }

    private static void RunCase(
        string name,
        Action test,
        ICollection<string> failures,
        TextWriter output)
    {
        try
        {
            test();
            output.WriteLine($"PASS  {name}");
        }
        catch (Exception exception)
        {
            failures.Add($"{name}: {exception.Message}");
            output.WriteLine($"FAIL  {name}");
        }
    }

    private static T Require<T>(T? value, string message)
        where T : class
    {
        return value ?? throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
