using CheckoutRefactoringKata.Contracts;
using CheckoutRefactoringKata.Starter;

namespace CheckoutRefactoringKata.Tests;

/// <summary>
/// 这些测试先于重构存在。它们不评价结构，只锁定调用方已经依赖的行为。
/// </summary>
public sealed class StarterCharacterizationTests
{
    [Fact]
    public void Member_checkout_produces_the_known_receipt_and_trace()
    {
        var payment = new RecordingPaymentGateway();
        var receipts = new RecordingReceiptStore();
        var service = new CheckoutService(payment, receipts);

        var result = service.Checkout(CheckoutFixture.ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Failure);
        Assert.Equal(
            new PricingBreakdown(120m, 12m, 0m, 108m),
            result.Receipt!.Pricing);
        Assert.Equal(CheckoutStatus.Completed, result.Receipt.Status);
        Assert.Equal(108m, payment.LastAmount);
        Assert.Single(receipts.Receipts);
        Assert.Equal(
            [
                "order:draft",
                "validation:passed",
                "order:validated",
                "pricing:Member",
                "payment:approved",
                "order:paid",
                "order:completed",
                "receipt:saved",
            ],
            result.Trace);
    }

    [Fact]
    public void Domestic_shipping_costs_twelve_when_discounted_goods_are_below_one_hundred()
    {
        var service = new CheckoutService(
            new RecordingPaymentGateway(),
            new RecordingReceiptStore());
        var request = new CheckoutRequest(
            "ORDER-SMALL",
            PricingPlan.Standard,
            [new CartItem("PEN", 25m, 2, 20)],
            "CN",
            "PAY-TEST",
            TermsAccepted: true);

        var result = service.Checkout(request);

        Assert.Equal(new PricingBreakdown(50m, 0m, 12m, 62m), result.Receipt!.Pricing);
    }

    [Fact]
    public void Validation_reports_terms_before_invalid_quantity()
    {
        var service = new CheckoutService(
            new RecordingPaymentGateway(),
            new RecordingReceiptStore());
        var request = CheckoutFixture.ValidRequest() with
        {
            TermsAccepted = false,
            Items = [new CartItem("BROKEN", 10m, 0, 0)],
        };

        var result = service.Checkout(request);

        Assert.Equal(CheckoutErrorCode.TermsNotAccepted, result.Failure!.Code);
        Assert.Equal(
            ["order:draft", "validation:failed:TermsNotAccepted"],
            result.Trace);
    }

    [Fact]
    public void Declined_payment_does_not_save_a_receipt()
    {
        var payment = new RecordingPaymentGateway(approved: false);
        var receipts = new RecordingReceiptStore();
        var service = new CheckoutService(payment, receipts);

        var result = service.Checkout(CheckoutFixture.ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(CheckoutErrorCode.PaymentDeclined, result.Failure!.Code);
        Assert.Empty(receipts.Receipts);
        Assert.Equal("payment:declined", result.Trace[^1]);
        Assert.DoesNotContain("order:paid", result.Trace);
    }
}
