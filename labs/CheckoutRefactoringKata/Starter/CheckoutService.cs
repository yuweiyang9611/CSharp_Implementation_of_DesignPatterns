using CheckoutRefactoringKata.Contracts;

namespace CheckoutRefactoringKata.Starter;

/// <summary>
/// 行为正确、测试可控，但同时知道校验、价格政策、状态机和用例编排。
/// 这正是工坊的起点：不是故意写错，而是让变化成本变得可观察。
/// </summary>
public sealed class CheckoutService(
    IPaymentGateway paymentGateway,
    IReceiptStore receiptStore)
{
    public CheckoutResult Checkout(CheckoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trace = new List<string> { "order:draft" };
        var status = CheckoutStatus.Draft;

        // 职责 1：所有业务校验和校验顺序都埋在用例服务中。
        CheckoutFailure? failure = null;

        if (request.Items.Count == 0)
        {
            failure = new(CheckoutErrorCode.EmptyCart, "购物车不能为空。");
        }
        else if (!request.TermsAccepted)
        {
            failure = new(CheckoutErrorCode.TermsNotAccepted, "必须接受结账条款。");
        }
        else if (request.Items.Any(item => item.Quantity <= 0))
        {
            failure = new(CheckoutErrorCode.InvalidQuantity, "商品数量必须大于 0。");
        }
        else if (request.Items.Any(item => item.Quantity > item.AvailableStock))
        {
            failure = new(CheckoutErrorCode.OutOfStock, "至少一件商品库存不足。");
        }
        else if (string.IsNullOrWhiteSpace(request.PaymentToken))
        {
            failure = new(CheckoutErrorCode.PaymentTokenMissing, "缺少支付令牌。");
        }

        if (failure is not null)
        {
            trace.Add($"validation:failed:{failure.Code}");
            return CheckoutResult.Failed(failure, trace);
        }

        trace.Add("validation:passed");

        // 职责 2：新增价格政策时必须修改这个 switch。
        var subtotal = decimal.Round(
            request.Items.Sum(item => item.UnitPrice * item.Quantity),
            2,
            MidpointRounding.AwayFromZero);

        var discountRate = request.PricingPlan switch
        {
            PricingPlan.Standard => 0m,
            PricingPlan.Member => 0.10m,
            PricingPlan.FlashSale => 0.20m,
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PricingPlan,
                "未知价格政策。"),
        };

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
        var pricing = new PricingBreakdown(
            subtotal,
            discount,
            shippingFee,
            discountedMerchandise + shippingFee);

        // 职责 3：服务自己维护状态转换；遗漏或乱序只能靠人工发现。
        status = CheckoutStatus.Validated;
        trace.Add("order:validated");
        trace.Add($"pricing:{request.PricingPlan}");

        // 职责 4：编排外部支付、状态转换和收据保存。
        var payment = paymentGateway.Charge(
            request.OrderId,
            pricing.Total,
            request.PaymentToken);

        if (!payment.Approved)
        {
            var declined = new CheckoutFailure(
                CheckoutErrorCode.PaymentDeclined,
                payment.DeclineReason ?? "支付被拒绝。");
            trace.Add("payment:declined");
            return CheckoutResult.Failed(declined, trace);
        }

        trace.Add("payment:approved");
        status = CheckoutStatus.Paid;
        trace.Add("order:paid");
        status = CheckoutStatus.Completed;
        trace.Add("order:completed");

        var receipt = new CheckoutReceipt(
            request.OrderId,
            request.PricingPlan,
            pricing,
            payment.TransactionId
                ?? throw new InvalidOperationException("成功的支付必须返回交易号。"),
            status);

        receiptStore.Save(receipt);
        trace.Add("receipt:saved");

        return CheckoutResult.Succeeded(receipt, trace);
    }
}
