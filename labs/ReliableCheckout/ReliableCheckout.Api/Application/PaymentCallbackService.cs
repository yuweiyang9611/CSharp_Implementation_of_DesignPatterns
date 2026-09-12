using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;
using ReliableCheckout.Payments;

namespace ReliableCheckout.Application;

public sealed class PaymentCallbackService(
    CheckoutDatabase database,
    CheckoutStore store,
    IClock clock,
    ILogger<PaymentCallbackService> logger,
    IPaymentGateway gateway)
{
    public async Task<ApplyCallbackResult> ApplyAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var signal = ParseOutcome(request.Outcome);
        var fingerprint = $"{request.OrderId}:{request.ExternalPaymentId}:{signal}";

        // Already committed callbacks remain replayable after upgrading an old database,
        // even when the former in-memory provider no longer has the payment in its ledger.
        await using (var preflight = await database.OpenConnectionAsync(cancellationToken))
        {
            var committed = await ConsumerReceipts.FindFingerprintAsync(preflight, null,
                "payment-webhook", request.EventId, cancellationToken);
            if (committed is not null)
            {
                if (!string.Equals(committed, fingerprint, StringComparison.Ordinal))
                    throw new IdempotencyConflictException("The webhook EventId was already used for a different payment result.");
                var replayed = await store.GetOrderAsync(request.OrderId, cancellationToken)
                    ?? throw new OrderNotFoundException(request.OrderId);
                return new(replayed, Replayed: true);
            }
        }

        // Provider and checkout have separate transactions. The provider arbitrates cancel/success races.
        var before = await store.GetOrderAsync(request.OrderId, cancellationToken) ?? throw new OrderNotFoundException(request.OrderId);
        if (before.PaymentStatus == PaymentStatus.PendingRequest)
            throw new InvalidStateTransitionException("payment", before.PaymentStatus.ToString(), signal.ToString());
        if (before.ExternalPaymentId != request.ExternalPaymentId) throw new PaymentIdentityMismatchException();
        await gateway.ObserveAsync(request.OrderId, request.ExternalPaymentId,
            signal == PaymentSignal.Succeeded ? ProviderStatus.Succeeded : ProviderStatus.Failed, cancellationToken);

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);

        var receipt = await ConsumerReceipts.FindFingerprintAsync(
            connection,
            transaction,
            "payment-webhook",
            request.EventId,
            cancellationToken);
        if (receipt is not null)
        {
            if (!string.Equals(receipt, fingerprint, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException(
                    "The webhook EventId was already used for a different payment result.");
            }

            await transaction.CommitAsync(cancellationToken);
            var replayed = await store.GetOrderAsync(request.OrderId, cancellationToken)
                ?? throw new OrderNotFoundException(request.OrderId);
            logger.LogInformation(
                "Ignored duplicate payment webhook {WebhookEventId} for order {OrderId}",
                request.EventId,
                request.OrderId);
            return new ApplyCallbackResult(replayed, Replayed: true);
        }

        var current = await ReadPaymentStateAsync(connection, transaction, request.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(request.OrderId);

        // Transition is checked before identity. A callback arriving before PaymentRequested is an
        // ordering error, even though no external id has been stored yet.
        var nextPayment = PaymentStateMachine.Apply(current.Payment, signal);
        var nextOrder = OrderStateMachine.ApplyPaymentResult(current.Order, signal);

        if (!string.Equals(current.ExternalPaymentId, request.ExternalPaymentId, StringComparison.Ordinal))
        {
            throw new PaymentIdentityMismatchException();
        }

        var now = clock.UtcNow;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE payments
                SET status = $paymentStatus, updated_at = $now
                WHERE order_id = $orderId;

                UPDATE orders
                SET status = $orderStatus, updated_at = $now
                WHERE id = $orderId;
                """;
            update.Parameters.AddWithValue("$paymentStatus", nextPayment.ToString());
            update.Parameters.AddWithValue("$orderStatus", nextOrder.ToString());
            update.Parameters.AddWithValue("$now", CheckoutStore.FormatTimestamp(now));
            update.Parameters.AddWithValue("$orderId", request.OrderId.ToString());
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await ConsumerReceipts.InsertAsync(
            connection,
            transaction,
            "payment-webhook",
            request.EventId,
            fingerprint,
            now,
            cancellationToken);

        await ReservationService.SettleAsync(connection, transaction, request.OrderId, nextOrder == OrderStatus.Paid, cancellationToken);

        var eventType = signal == PaymentSignal.Succeeded ? "OrderPaid" : "OrderPaymentFailed";
        var payload = JsonSerializer.Serialize(new OrderPaymentResultEvent(request.OrderId, nextOrder.ToString()));
        await CheckoutStore.InsertOutboxAsync(
            connection,
            transaction,
            Guid.NewGuid(),
            eventType,
            request.OrderId,
            payload,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var changed = await store.GetOrderAsync(request.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(request.OrderId);

        logger.LogInformation(
            "Applied payment webhook {WebhookEventId}; order {OrderId} moved to {OrderStatus}",
            request.EventId,
            request.OrderId,
            nextOrder);
        return new ApplyCallbackResult(changed, Replayed: false);
    }

    private static PaymentSignal ParseOutcome(string outcome) => outcome.Trim().ToUpperInvariant() switch
    {
        "SUCCEEDED" => PaymentSignal.Succeeded,
        "FAILED" => PaymentSignal.Failed,
        _ => throw new ArgumentException("Outcome must be 'succeeded' or 'failed'.", nameof(outcome))
    };

    private static async Task<(OrderStatus Order, PaymentStatus Payment, string? ExternalPaymentId)?> ReadPaymentStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT o.status, p.status, p.external_payment_id
            FROM orders o JOIN payments p ON p.order_id = o.id
            WHERE o.id = $orderId;
            """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (
            Enum.Parse<OrderStatus>(reader.GetString(0)),
            Enum.Parse<PaymentStatus>(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }
}

internal static class ConsumerReceipts
{
    public static async Task<string?> FindFingerprintAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string consumer,
        string eventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT fingerprint FROM consumer_receipts
            WHERE consumer = $consumer AND event_id = $eventId;
            """;
        command.Parameters.AddWithValue("$consumer", consumer);
        command.Parameters.AddWithValue("$eventId", eventId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public static async Task InsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string consumer,
        string eventId,
        string fingerprint,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO consumer_receipts(consumer, event_id, fingerprint, processed_at)
            VALUES ($consumer, $eventId, $fingerprint, $processedAt);
            """;
        command.Parameters.AddWithValue("$consumer", consumer);
        command.Parameters.AddWithValue("$eventId", eventId);
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        command.Parameters.AddWithValue("$processedAt", CheckoutStore.FormatTimestamp(processedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
