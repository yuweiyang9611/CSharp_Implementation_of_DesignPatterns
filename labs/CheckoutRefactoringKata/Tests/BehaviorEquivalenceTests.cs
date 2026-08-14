using CheckoutRefactoringKata.Contracts;
using CheckoutRefactoringKata.Reference.Application;
using CheckoutRefactoringKata.Starter;

namespace CheckoutRefactoringKata.Tests;

/// <summary>
/// 同一输入、同一外部响应下，Starter 与 Reference 必须产生相同输出和轨迹。
/// </summary>
public sealed class BehaviorEquivalenceTests
{
    public static TheoryData<CheckoutRequest, bool> Scenarios =>
        new()
        {
            { CheckoutFixture.ValidRequest(PricingPlan.Standard), true },
            { CheckoutFixture.ValidRequest(PricingPlan.Member), true },
            { CheckoutFixture.ValidRequest(PricingPlan.FlashSale), true },
            {
                new CheckoutRequest(
                    "ORDER-INTL",
                    PricingPlan.Standard,
                    [new CartItem("PEN", 25m, 2, 10)],
                    "JP",
                    "PAY-TEST",
                    TermsAccepted: true),
                true
            },
            { CheckoutFixture.ValidRequest() with { Items = [] }, true },
            { CheckoutFixture.ValidRequest() with { TermsAccepted = false }, true },
            {
                CheckoutFixture.ValidRequest() with
                {
                    Items = [new CartItem("BAD-QTY", 10m, 0, 10)],
                },
                true
            },
            {
                CheckoutFixture.ValidRequest() with
                {
                    Items = [new CartItem("NO-STOCK", 10m, 2, 1)],
                },
                true
            },
            { CheckoutFixture.ValidRequest() with { PaymentToken = " " }, true },
            { CheckoutFixture.ValidRequest(), false },
        };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Refactoring_preserves_observable_behavior(
        CheckoutRequest request,
        bool paymentApproved)
    {
        var starter = new CheckoutService(
            new RecordingPaymentGateway(paymentApproved),
            new RecordingReceiptStore());
        var reference = CheckoutFacade.CreateDefault(
            new RecordingPaymentGateway(paymentApproved),
            new RecordingReceiptStore());

        var before = starter.Checkout(request);
        var after = reference.Checkout(request);

        CheckoutResultAssertions.Equivalent(before, after);
    }
}
