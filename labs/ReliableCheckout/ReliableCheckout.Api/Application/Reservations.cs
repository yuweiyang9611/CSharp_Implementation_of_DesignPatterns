using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;
using ReliableCheckout.Messaging;
using ReliableCheckout.Payments;

namespace ReliableCheckout.Application;

public sealed class ReservationService(CheckoutDatabase database, IClock clock)
{
    public async Task<int> ExpireAsync(CancellationToken token = default)
    {
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var ids = new List<Guid>();
        await using (var command = Sql.Command(connection, transaction, """
            SELECT o.id FROM orders o JOIN reservations r ON r.order_id = o.id
            WHERE o.status = 'AwaitingPayment' AND r.state = 'Held' AND r.expires_at <= $now LIMIT 20;
            """, ("$now", CheckoutStore.FormatTimestamp(clock.UtcNow))))
        await using (var reader = await command.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token)) ids.Add(Guid.Parse(reader.GetString(0)));
        foreach (var id in ids)
        {
            await Sql.ExecuteAsync(connection, transaction,
                "UPDATE orders SET status = 'CancellationPending', updated_at = $now WHERE id = $id AND status = 'AwaitingPayment';",
                token, ("$id", id.ToString()), ("$now", CheckoutStore.FormatTimestamp(clock.UtcNow)));
            await CheckoutStore.InsertOutboxAsync(connection, transaction, Guid.NewGuid(), "CancelPayment", id,
                JsonSerializer.Serialize(new { OrderId = id }), clock.UtcNow, token);
        }
        await transaction.CommitAsync(token);
        return ids.Count;
    }
    internal static async Task SettleAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid orderId, bool paid, CancellationToken token)
    {
        if (!paid)
            await Sql.ExecuteAsync(connection, transaction, """
                UPDATE inventory SET available = available + (SELECT quantity FROM orders WHERE id = $id)
                WHERE sku = (SELECT sku FROM orders WHERE id = $id)
                    AND EXISTS(SELECT 1 FROM reservations WHERE order_id = $id AND state = 'Held');
                """, token, ("$id", orderId.ToString()));
        await Sql.ExecuteAsync(connection, transaction,
            "UPDATE reservations SET state = $state WHERE order_id = $id AND state = 'Held';", token,
            ("$id", orderId.ToString()), ("$state", paid ? "Consumed" : "Released"));
    }
    public async Task ApplyProviderResultAsync(Guid orderId, PaymentLookup result, CancellationToken token)
    {
        var signal = result.Status switch
        {
            ProviderStatus.Succeeded => PaymentSignal.Succeeded,
            ProviderStatus.Failed => PaymentSignal.Failed,
            ProviderStatus.Cancelled => PaymentSignal.Cancelled,
            _ => throw new InvalidOperationException("Provider result is unknown; inventory remains reserved.")
        };
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        OrderStatus currentOrder;
        PaymentStatus currentPayment;
        await using (var command = Sql.Command(connection, transaction,
            "SELECT o.status, p.status FROM orders o JOIN payments p ON p.order_id = o.id WHERE o.id = $id;", ("$id", orderId.ToString())))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) throw new OrderNotFoundException(orderId);
            currentOrder = Enum.Parse<OrderStatus>(reader.GetString(0));
            currentPayment = Enum.Parse<PaymentStatus>(reader.GetString(1));
        }
        var nextOrder = OrderStateMachine.ApplyPaymentResult(currentOrder, signal);
        var nextPayment = currentPayment == PaymentStatus.PendingRequest && signal == PaymentSignal.Failed
            ? PaymentStatus.Failed : PaymentStateMachine.Apply(currentPayment, signal);
        await Sql.ExecuteAsync(connection, transaction, """
            UPDATE orders SET status = $orderStatus, updated_at = $now WHERE id = $id;
            UPDATE payments SET status = $paymentStatus, external_payment_id = $external, updated_at = $now WHERE order_id = $id;
            """, token, ("$id", orderId.ToString()), ("$orderStatus", nextOrder.ToString()),
            ("$paymentStatus", nextPayment.ToString()), ("$external", result.PaymentId), ("$now", CheckoutStore.FormatTimestamp(clock.UtcNow)));
        await SettleAsync(connection, transaction, orderId, nextOrder == OrderStatus.Paid, token);
        if (currentOrder != nextOrder)
            await CheckoutStore.InsertOutboxAsync(connection, transaction, Guid.NewGuid(), "OrderSettled", orderId,
                JsonSerializer.Serialize(new OrderPaymentResultEvent(orderId, nextOrder.ToString())), clock.UtcNow, token);
        await transaction.CommitAsync(token);
    }
}
public sealed class CancelPaymentHandler(IPaymentGateway gateway, ReservationService reservations) : IOutboxHandler
{
    public string MessageType => "CancelPayment";
    public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var result = await gateway.QueryAsync(message.AggregateId, cancellationToken);
        if (result.Status is ProviderStatus.Pending or ProviderStatus.Unknown)
            result = await gateway.CancelAsync(message.AggregateId, cancellationToken);
        await reservations.ApplyProviderResultAsync(message.AggregateId, result, cancellationToken);
    }
}
