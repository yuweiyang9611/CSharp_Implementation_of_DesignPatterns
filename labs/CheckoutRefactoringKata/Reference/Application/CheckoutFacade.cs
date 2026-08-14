using CheckoutRefactoringKata.Contracts;
using CheckoutRefactoringKata.Reference.Orders;
using CheckoutRefactoringKata.Reference.Pricing;
using CheckoutRefactoringKata.Reference.Validation;

namespace CheckoutRefactoringKata.Reference.Application;

/// <summary>
/// Facade 为调用方提供一个稳定入口，并负责协调校验链、价格策略、订单状态和外部端口。
/// </summary>
public sealed class CheckoutFacade(
    CheckoutValidationChain validationChain,
    PricingStrategyResolver pricingStrategies,
    IPaymentGateway paymentGateway,
    IReceiptStore receiptStore)
{
    public CheckoutResult Checkout(CheckoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trace = new List<string>();
        var order = new CheckoutOrder(request.OrderId, trace);
        var failure = validationChain.Validate(request);

        if (failure is not null)
        {
            trace.Add($"validation:failed:{failure.Code}");
            return CheckoutResult.Failed(failure, trace);
        }

        trace.Add("validation:passed");
        order.MarkValidated();

        var pricing = pricingStrategies
            .Resolve(request.PricingPlan)
            .Calculate(request);
        trace.Add($"pricing:{request.PricingPlan}");

        var payment = paymentGateway.Charge(
            order.OrderId,
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
        order.MarkPaid();
        order.Complete();

        var receipt = new CheckoutReceipt(
            order.OrderId,
            request.PricingPlan,
            pricing,
            payment.TransactionId
                ?? throw new InvalidOperationException("成功的支付必须返回交易号。"),
            order.Status);

        receiptStore.Save(receipt);
        trace.Add("receipt:saved");

        return CheckoutResult.Succeeded(receipt, trace);
    }

    public static CheckoutFacade CreateDefault(
        IPaymentGateway paymentGateway,
        IReceiptStore receiptStore) =>
        new(
            CheckoutValidationChain.CreateDefault(),
            PricingStrategyResolver.CreateDefault(),
            paymentGateway,
            receiptStore);
}
