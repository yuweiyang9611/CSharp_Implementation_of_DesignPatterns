using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;

namespace ReliableCheckout.Application;

public sealed class CheckoutStore(CheckoutDatabase database, IClock clock, ILogger<CheckoutStore> logger)
{
    public async Task<CreateOrderResult> CreateOrderAsync(
        string idempotencyKey,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        var fingerprint = $"{sku}:{request.Quantity}";

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);

        var existing = await FindByIdempotencyKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Value.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException(
                    "The Idempotency-Key was already used with a different request body.");
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Replayed checkout request {IdempotencyKey} for order {OrderId}",
                idempotencyKey,
                existing.Value.Order.Id);
            return new CreateOrderResult(existing.Value.Order, Replayed: true);
        }

        var unitPrice = await ReadUnitPriceAsync(connection, transaction, sku, cancellationToken);
        if (unitPrice is null)
        {
            throw new InsufficientStockException(sku);
        }

        await using (var reserve = connection.CreateCommand())
        {
            reserve.Transaction = transaction;
            reserve.CommandText = """
                UPDATE inventory
                SET available = available - $quantity
                WHERE sku = $sku AND available >= $quantity;
                """;
            reserve.Parameters.AddWithValue("$sku", sku);
            reserve.Parameters.AddWithValue("$quantity", request.Quantity);
            if (await reserve.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InsufficientStockException(sku);
            }
        }

        var now = clock.UtcNow;
        var orderId = Guid.NewGuid();
        var total = checked(unitPrice.Value * request.Quantity);

        await InsertOrderAsync(
            connection,
            transaction,
            orderId,
            idempotencyKey,
            fingerprint,
            sku,
            request.Quantity,
            unitPrice.Value,
            total,
            now,
            cancellationToken);

        var outboxId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new PaymentRequestedEvent(orderId, total));
        await InsertOutboxAsync(
            connection,
            transaction,
            outboxId,
            "PaymentRequested",
            orderId,
            payload,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var created = new OrderSnapshot(
            orderId,
            sku,
            request.Quantity,
            unitPrice.Value,
            total,
            OrderStatus.AwaitingPayment,
            PaymentStatus.PendingRequest,
            null,
            now,
            now);

        logger.LogInformation(
            "Created order {OrderId}; reserved {Quantity} of {Sku}; outbox event {OutboxId}",
            orderId,
            request.Quantity,
            sku,
            outboxId);
        return new CreateOrderResult(created, Replayed: false);
    }

    public async Task<OrderSnapshot?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        return await FindOrderAsync(connection, null, "o.id = $value", orderId.ToString(), cancellationToken);
    }

    public async Task SetInventoryAsync(
        string sku,
        int available,
        long unitPriceCents = 1999,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(available);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitPriceCents);

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO inventory(sku, available, unit_price_cents)
            VALUES ($sku, $available, $price)
            ON CONFLICT(sku) DO UPDATE SET
                available = excluded.available,
                unit_price_cents = excluded.unit_price_cents;
            """;
        command.Parameters.AddWithValue("$sku", sku.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("$available", available);
        command.Parameters.AddWithValue("$price", unitPriceCents);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int?> GetInventoryAsync(string sku, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT available FROM inventory WHERE sku = $sku;";
        command.Parameters.AddWithValue("$sku", sku.Trim().ToUpperInvariant());
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<OutboxInspection>> GetOutboxAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<OutboxInspection>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, type, attempts, next_attempt_at, processed_at, last_error
            FROM outbox ORDER BY occurred_at;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OutboxInspection(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetInt32(2),
                ReadNullableTimestamp(reader, 3),
                ReadNullableTimestamp(reader, 4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return results;
    }

    internal static async Task InsertOutboxAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid id,
        string type,
        Guid aggregateId,
        string payload,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO outbox(id, type, aggregate_id, payload, occurred_at)
            VALUES ($id, $type, $aggregateId, $payload, $occurredAt);
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$aggregateId", aggregateId.ToString());
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$occurredAt", FormatTimestamp(occurredAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static string FormatTimestamp(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    internal static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? ReadNullableTimestamp(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseTimestamp(reader.GetString(ordinal));

    private static async Task InsertOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid orderId,
        string idempotencyKey,
        string fingerprint,
        string sku,
        int quantity,
        long unitPrice,
        long total,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = """
            INSERT INTO orders(
                id, idempotency_key, request_fingerprint, sku, quantity,
                unit_price_cents, total_cents, status, created_at, updated_at)
            VALUES (
                $id, $key, $fingerprint, $sku, $quantity,
                $unitPrice, $total, $status, $now, $now);
            """;
        orderCommand.Parameters.AddWithValue("$id", orderId.ToString());
        orderCommand.Parameters.AddWithValue("$key", idempotencyKey);
        orderCommand.Parameters.AddWithValue("$fingerprint", fingerprint);
        orderCommand.Parameters.AddWithValue("$sku", sku);
        orderCommand.Parameters.AddWithValue("$quantity", quantity);
        orderCommand.Parameters.AddWithValue("$unitPrice", unitPrice);
        orderCommand.Parameters.AddWithValue("$total", total);
        orderCommand.Parameters.AddWithValue("$status", OrderStatus.AwaitingPayment.ToString());
        orderCommand.Parameters.AddWithValue("$now", FormatTimestamp(now));
        await orderCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var paymentCommand = connection.CreateCommand();
        paymentCommand.Transaction = transaction;
        paymentCommand.CommandText = """
            INSERT INTO payments(order_id, status, updated_at)
            VALUES ($orderId, $status, $now);
            """;
        paymentCommand.Parameters.AddWithValue("$orderId", orderId.ToString());
        paymentCommand.Parameters.AddWithValue("$status", PaymentStatus.PendingRequest.ToString());
        paymentCommand.Parameters.AddWithValue("$now", FormatTimestamp(now));
        await paymentCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long?> ReadUnitPriceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sku,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT unit_price_cents FROM inventory WHERE sku = $sku;";
        command.Parameters.AddWithValue("$sku", sku);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<(OrderSnapshot Order, string Fingerprint)?> FindByIdempotencyKeyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = BuildOrderQuery(connection, transaction, "o.idempotency_key = $value", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (ReadOrder(reader), reader.GetString(11));
    }

    private static async Task<OrderSnapshot?> FindOrderAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string predicate,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = BuildOrderQuery(connection, transaction, predicate, value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadOrder(reader) : null;
    }

    private static SqliteCommand BuildOrderQuery(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string predicate,
        string value)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT
                o.id, o.sku, o.quantity, o.unit_price_cents, o.total_cents,
                o.status, p.status, p.external_payment_id, o.created_at, o.updated_at,
                o.idempotency_key, o.request_fingerprint
            FROM orders o
            JOIN payments p ON p.order_id = o.id
            WHERE {predicate};
            """;
        command.Parameters.AddWithValue("$value", value);
        return command;
    }

    private static OrderSnapshot ReadOrder(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetInt32(2),
        reader.GetInt64(3),
        reader.GetInt64(4),
        Enum.Parse<OrderStatus>(reader.GetString(5)),
        Enum.Parse<PaymentStatus>(reader.GetString(6)),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        ParseTimestamp(reader.GetString(8)),
        ParseTimestamp(reader.GetString(9)));
}
