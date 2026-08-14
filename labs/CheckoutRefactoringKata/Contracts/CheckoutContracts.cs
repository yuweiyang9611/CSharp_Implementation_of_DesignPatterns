namespace CheckoutRefactoringKata.Contracts;

/// <summary>
/// 选择哪一套价格政策。它是教学示例中的业务概念，而不是模式名称。
/// </summary>
public enum PricingPlan
{
    Standard,
    Member,
    FlashSale,
}

public enum CheckoutStatus
{
    Draft,
    Validated,
    Paid,
    Completed,
}

public enum CheckoutErrorCode
{
    EmptyCart,
    TermsNotAccepted,
    InvalidQuantity,
    OutOfStock,
    PaymentTokenMissing,
    PaymentDeclined,
}

public sealed record CartItem(
    string Sku,
    decimal UnitPrice,
    int Quantity,
    int AvailableStock);

public sealed record CheckoutRequest(
    string OrderId,
    PricingPlan PricingPlan,
    IReadOnlyList<CartItem> Items,
    string ShippingCountry,
    string PaymentToken,
    bool TermsAccepted);

public sealed record PricingBreakdown(
    decimal Subtotal,
    decimal Discount,
    decimal ShippingFee,
    decimal Total);

public sealed record CheckoutFailure(CheckoutErrorCode Code, string Message);

public sealed record CheckoutReceipt(
    string OrderId,
    PricingPlan PricingPlan,
    PricingBreakdown Pricing,
    string TransactionId,
    CheckoutStatus Status);

/// <summary>
/// 成功和失败都携带轨迹，便于特征测试锁定可观察行为。
/// </summary>
public sealed record CheckoutResult(
    CheckoutReceipt? Receipt,
    CheckoutFailure? Failure,
    IReadOnlyList<string> Trace)
{
    public bool IsSuccess => Receipt is not null;

    public static CheckoutResult Succeeded(
        CheckoutReceipt receipt,
        IEnumerable<string> trace) =>
        new(receipt, null, trace.ToArray());

    public static CheckoutResult Failed(
        CheckoutFailure failure,
        IEnumerable<string> trace) =>
        new(null, failure, trace.ToArray());
}

public sealed record PaymentDecision(
    bool Approved,
    string? TransactionId,
    string? DeclineReason)
{
    public static PaymentDecision Approve(string transactionId) =>
        new(true, transactionId, null);

    public static PaymentDecision Decline(string reason) =>
        new(false, null, reason);
}

/// <summary>
/// 支付是慢且有副作用的系统边界；Starter 也保留这个缝，测试才不必访问真实支付系统。
/// </summary>
public interface IPaymentGateway
{
    PaymentDecision Charge(string orderId, decimal amount, string paymentToken);
}

public interface IReceiptStore
{
    void Save(CheckoutReceipt receipt);
}
