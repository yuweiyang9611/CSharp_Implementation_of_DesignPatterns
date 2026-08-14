namespace ReliableCheckout.Domain;

public sealed record OrderSnapshot(
    Guid Id,
    string Sku,
    int Quantity,
    long UnitPriceCents,
    long TotalCents,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string? ExternalPaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OutboxEnvelope(
    Guid Id,
    string Type,
    Guid AggregateId,
    string Payload,
    int Attempts,
    DateTimeOffset OccurredAt);

public sealed record OutboxInspection(
    Guid Id,
    string Type,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? ProcessedAt,
    string? LastError);

public sealed record PaymentRequestedEvent(Guid OrderId, long TotalCents);

public sealed record OrderPaymentResultEvent(Guid OrderId, string Status);
