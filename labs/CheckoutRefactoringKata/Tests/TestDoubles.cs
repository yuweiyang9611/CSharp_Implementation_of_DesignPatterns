using CheckoutRefactoringKata.Contracts;

namespace CheckoutRefactoringKata.Tests;

internal sealed class RecordingPaymentGateway(
    bool approved = true,
    string declineReason = "发卡行拒绝了支付。") : IPaymentGateway
{
    public int ChargeCount { get; private set; }

    public decimal? LastAmount { get; private set; }

    public PaymentDecision Charge(string orderId, decimal amount, string paymentToken)
    {
        ChargeCount++;
        LastAmount = amount;

        return approved
            ? PaymentDecision.Approve($"TX-{orderId}-{amount:0.00}")
            : PaymentDecision.Decline(declineReason);
    }
}

internal sealed class RecordingReceiptStore : IReceiptStore
{
    public List<CheckoutReceipt> Receipts { get; } = [];

    public void Save(CheckoutReceipt receipt) => Receipts.Add(receipt);
}

internal static class CheckoutFixture
{
    public static CheckoutRequest ValidRequest(
        PricingPlan plan = PricingPlan.Member,
        decimal firstUnitPrice = 80m,
        string shippingCountry = "CN") =>
        new(
            "ORDER-TEST-001",
            plan,
            [
                new CartItem("BOOK-DP", firstUnitPrice, 1, 10),
                new CartItem("CARD", 20m, 2, 10),
            ],
            shippingCountry,
            "PAY-TEST",
            TermsAccepted: true);
}

internal static class CheckoutResultAssertions
{
    public static void Equivalent(CheckoutResult expected, CheckoutResult actual)
    {
        Assert.Equal(expected.IsSuccess, actual.IsSuccess);
        Assert.Equal(expected.Failure, actual.Failure);
        Assert.Equal(expected.Receipt, actual.Receipt);
        Assert.Equal(expected.Trace.ToArray(), actual.Trace.ToArray());
    }
}
