using System.Text.Json;
using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;

namespace ReliableCheckout.Messaging;

/// <summary>
/// A small read-model consumer that demonstrates inbox/receipt-based idempotency.
/// </summary>
public sealed class OrderProjectionHandler(
    CheckoutDatabase database,
    IClock clock,
    ILogger<OrderProjectionHandler> logger) : IOutboxHandler
{
    public OrderProjectionHandler(
        string messageType,
        CheckoutDatabase database,
        IClock clock,
        ILogger<OrderProjectionHandler> logger)
        : this(database, clock, logger)
    {
        MessageType = messageType;
    }

    public string MessageType { get; } = "OrderPaid";

    public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var result = JsonSerializer.Deserialize<OrderPaymentResultEvent>(message.Payload)
            ?? throw new InvalidOperationException($"{MessageType} payload is invalid.");

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var receipt = await ConsumerReceipts.FindFingerprintAsync(
            connection,
            transaction,
            $"projection:{MessageType}",
            message.Id.ToString(),
            cancellationToken);
        if (receipt is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = clock.UtcNow;
        await using (var projection = connection.CreateCommand())
        {
            projection.Transaction = transaction;
            projection.CommandText = """
                INSERT INTO order_projection(order_id, status, source_event_id, updated_at)
                VALUES ($orderId, $status, $eventId, $now)
                ON CONFLICT(order_id) DO UPDATE SET
                    status = excluded.status,
                    source_event_id = excluded.source_event_id,
                    updated_at = excluded.updated_at;
                """;
            projection.Parameters.AddWithValue("$orderId", result.OrderId.ToString());
            projection.Parameters.AddWithValue("$status", result.Status);
            projection.Parameters.AddWithValue("$eventId", message.Id.ToString());
            projection.Parameters.AddWithValue("$now", CheckoutStore.FormatTimestamp(now));
            await projection.ExecuteNonQueryAsync(cancellationToken);
        }

        await ConsumerReceipts.InsertAsync(
            connection,
            transaction,
            $"projection:{MessageType}",
            message.Id.ToString(),
            $"{result.OrderId}:{result.Status}",
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Projection consumed {MessageType} event {EventId} for order {OrderId}",
            MessageType,
            message.Id,
            result.OrderId);
    }
}
