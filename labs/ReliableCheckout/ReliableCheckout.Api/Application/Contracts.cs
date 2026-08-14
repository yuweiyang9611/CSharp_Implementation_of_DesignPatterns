using ReliableCheckout.Domain;

namespace ReliableCheckout.Application;

public sealed record CreateOrderRequest(string Sku, int Quantity);

public sealed record PaymentWebhookRequest(
    string EventId,
    Guid OrderId,
    string ExternalPaymentId,
    string Outcome);

public sealed record OrderResponse(
    Guid Id,
    string Sku,
    int Quantity,
    long UnitPriceCents,
    long TotalCents,
    string Status,
    string PaymentStatus,
    string? ExternalPaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool Replayed = false)
{
    public static OrderResponse From(OrderSnapshot order, bool replayed = false) => new(
        order.Id,
        order.Sku,
        order.Quantity,
        order.UnitPriceCents,
        order.TotalCents,
        order.Status.ToString(),
        order.PaymentStatus.ToString(),
        order.ExternalPaymentId,
        order.CreatedAt,
        order.UpdatedAt,
        replayed);
}

public sealed record ApiError(string Code, string Message);

public sealed record CreateOrderResult(OrderSnapshot Order, bool Replayed);

public sealed record ApplyCallbackResult(OrderSnapshot Order, bool Replayed);

public sealed class IdempotencyConflictException(string message) : InvalidOperationException(message);

public sealed class InsufficientStockException(string sku) : InvalidOperationException($"Insufficient stock for '{sku}'.");

public sealed class OrderNotFoundException(Guid orderId) : InvalidOperationException($"Order '{orderId}' was not found.");

public sealed class PaymentIdentityMismatchException : InvalidOperationException
{
    public PaymentIdentityMismatchException() : base("External payment id does not belong to this order.")
    {
    }
}
