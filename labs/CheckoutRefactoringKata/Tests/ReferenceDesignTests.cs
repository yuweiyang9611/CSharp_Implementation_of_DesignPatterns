using CheckoutRefactoringKata.Contracts;
using CheckoutRefactoringKata.Reference.Application;
using CheckoutRefactoringKata.Reference.Orders;
using CheckoutRefactoringKata.Reference.Pricing;
using CheckoutRefactoringKata.Reference.Validation;

namespace CheckoutRefactoringKata.Tests;

public sealed class ReferenceDesignTests
{
    [Theory]
    [InlineData(PricingPlan.Standard, 0)]
    [InlineData(PricingPlan.Member, 12)]
    [InlineData(PricingPlan.FlashSale, 24)]
    public void Resolver_selects_the_expected_discount_strategy(
        PricingPlan plan,
        decimal expectedDiscount)
    {
        var strategy = PricingStrategyResolver.CreateDefault().Resolve(plan);

        var pricing = strategy.Calculate(CheckoutFixture.ValidRequest(plan));

        Assert.Equal(plan, strategy.Plan);
        Assert.Equal(expectedDiscount, pricing.Discount);
    }

    [Fact]
    public void Validation_chain_stops_at_the_first_failure()
    {
        var request = CheckoutFixture.ValidRequest() with
        {
            TermsAccepted = false,
            Items = [new CartItem("BAD", 10m, 0, 0)],
            PaymentToken = string.Empty,
        };

        var failure = CheckoutValidationChain.CreateDefault().Validate(request);

        Assert.Equal(CheckoutErrorCode.TermsNotAccepted, failure!.Code);
    }

    [Fact]
    public void State_object_allows_only_draft_validated_paid_completed_order()
    {
        var trace = new List<string>();
        var order = new CheckoutOrder("ORDER-STATE", trace);

        order.MarkValidated();
        order.MarkPaid();
        order.Complete();

        Assert.Equal(CheckoutStatus.Completed, order.Status);
        Assert.Equal(
            ["order:draft", "order:validated", "order:paid", "order:completed"],
            trace);
    }

    [Fact]
    public void State_object_rejects_completion_from_draft()
    {
        var order = new CheckoutOrder("ORDER-STATE", new List<string>());

        var exception = Assert.Throws<InvalidOperationException>(order.Complete);

        Assert.Contains("Draft", exception.Message);
        Assert.Equal(CheckoutStatus.Draft, order.Status);
    }

    [Fact]
    public void Validation_failure_never_calls_payment_or_receipt_ports()
    {
        var payment = new RecordingPaymentGateway();
        var receipts = new RecordingReceiptStore();
        var facade = CheckoutFacade.CreateDefault(payment, receipts);

        var result = facade.Checkout(
            CheckoutFixture.ValidRequest() with { Items = [] });

        Assert.Equal(CheckoutErrorCode.EmptyCart, result.Failure!.Code);
        Assert.Equal(0, payment.ChargeCount);
        Assert.Empty(receipts.Receipts);
    }

    [Fact]
    public void Payment_failure_leaves_order_unpaid_and_does_not_save_receipt()
    {
        var receipts = new RecordingReceiptStore();
        var facade = CheckoutFacade.CreateDefault(
            new RecordingPaymentGateway(approved: false),
            receipts);

        var result = facade.Checkout(CheckoutFixture.ValidRequest());

        Assert.Equal(CheckoutErrorCode.PaymentDeclined, result.Failure!.Code);
        Assert.Empty(receipts.Receipts);
        Assert.Equal("payment:declined", result.Trace[^1]);
        Assert.DoesNotContain("order:paid", result.Trace);
        Assert.DoesNotContain("order:completed", result.Trace);
    }
}
