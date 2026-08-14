using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;
using ReliableCheckout.Payments;

namespace ReliableCheckout.Messaging;

public sealed class PaymentRequestedHandler(
    CheckoutDatabase database,
    IPaymentGateway gateway,
    IClock clock,
    ILogger<PaymentRequestedHandler> logger) : IOutboxHandler
{
    private const string ConsumerName = "payment-requested";

    public string MessageType => "PaymentRequested";

    public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var paymentRequest = JsonSerializer.Deserialize<PaymentRequestedEvent>(message.Payload)
            ?? throw new InvalidOperationException("PaymentRequested payload is invalid.");
        var fingerprint = $"{paymentRequest.OrderId}:{paymentRequest.TotalCents}";

        var existingFingerprint = await FindReceiptFingerprintAsync(message.Id, cancellationToken);
        if (existingFingerprint is not null)
        {
            EnsureMatchingFingerprint(message.Id, existingFingerprint, fingerprint);
            logger.LogInformation(
                "Consumer {Consumer} ignored duplicate event {EventId}",
                ConsumerName,
                message.Id);
            return;
        }

        // The provider receives the outbox id as its idempotency key. If this process crashes
        // after the external call, a retry cannot create a second charge.
        var started = await gateway.StartAsync(
            paymentRequest.OrderId,
            paymentRequest.TotalCents,
            message.Id,
            cancellationToken);

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        existingFingerprint = await ConsumerReceipts.FindFingerprintAsync(
                connection,
                transaction,
                ConsumerName,
                message.Id.ToString(),
                cancellationToken);
        if (existingFingerprint is not null)
        {
            EnsureMatchingFingerprint(message.Id, existingFingerprint, fingerprint);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var current = await ReadPaymentStatusAsync(
            connection,
            transaction,
            paymentRequest.OrderId,
            cancellationToken);
        var next = PaymentStateMachine.Apply(current, PaymentSignal.RequestAccepted);
        var now = clock.UtcNow;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE payments
                SET status = $status, external_payment_id = $externalId, updated_at = $now
                WHERE order_id = $orderId;

                UPDATE orders SET updated_at = $now WHERE id = $orderId;
                """;
            update.Parameters.AddWithValue("$status", next.ToString());
            update.Parameters.AddWithValue("$externalId", started.ExternalPaymentId);
            update.Parameters.AddWithValue("$now", CheckoutStore.FormatTimestamp(now));
            update.Parameters.AddWithValue("$orderId", paymentRequest.OrderId.ToString());
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await ConsumerReceipts.InsertAsync(
            connection,
            transaction,
            ConsumerName,
            message.Id.ToString(),
            fingerprint,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Payment provider accepted order {OrderId} as {ExternalPaymentId} for event {EventId}",
            paymentRequest.OrderId,
            started.ExternalPaymentId,
            message.Id);
    }

    private async Task<string?> FindReceiptFingerprintAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var fingerprint = await ConsumerReceipts.FindFingerprintAsync(
            connection,
            transaction,
            ConsumerName,
            eventId.ToString(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return fingerprint;
    }

    private static void EnsureMatchingFingerprint(Guid eventId, string existing, string current)
    {
        if (!string.Equals(existing, current, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Outbox event '{eventId}' was replayed with a different payload fingerprint.");
        }
    }

    private static async Task<PaymentStatus> ReadPaymentStatusAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT status FROM payments WHERE order_id = $orderId;";
        command.Parameters.AddWithValue("$orderId", orderId.ToString());
        var value = await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new OrderNotFoundException(orderId);
        return Enum.Parse<PaymentStatus>(value);
    }
}
