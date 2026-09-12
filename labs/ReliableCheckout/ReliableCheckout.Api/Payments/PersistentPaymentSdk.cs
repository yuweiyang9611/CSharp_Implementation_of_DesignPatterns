using Microsoft.Data.Sqlite;
using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;

namespace ReliableCheckout.Payments;

/// <summary>A separate provider ledger. Cancel-before-create stores a durable tombstone.</summary>
public sealed class PersistentPaymentSdk
{
    public string ConnectionString { get; }
    public PersistentPaymentSdk(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration.GetConnectionString("PaymentProvider");
        if (string.IsNullOrWhiteSpace(configured))
        {
            var checkout = new SqliteConnectionStringBuilder(configuration.GetConnectionString("Checkout"));
            var fallback = new SqliteConnectionStringBuilder { DataSource = checkout.DataSource + ".provider.db", Pooling = false, DefaultTimeout = 10 };
            configured = fallback.ToString();
        }
        var builder = new SqliteConnectionStringBuilder(configured);
        if (!Path.IsPathRooted(builder.DataSource)) builder.DataSource = Path.Combine(environment.ContentRootPath, builder.DataSource);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(builder.DataSource))!);
        ConnectionString = builder.ToString();
    }
    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(token);
        await Sql.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS provider_payments (
                order_id TEXT PRIMARY KEY, payment_id TEXT NOT NULL UNIQUE,
                status TEXT NOT NULL, created INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS provider_requests (
                idempotency_key TEXT PRIMARY KEY, order_id TEXT NOT NULL,
                amount_cents INTEGER NOT NULL, payment_id TEXT NOT NULL);
            """, token);
        return connection;
    }
    public int RequestCount
    {
        get
        {
            using var connection = OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
            using var command = Sql.Command(connection, null, "SELECT COUNT(*) FROM provider_payments WHERE created = 1;");
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }
    public async Task<LegacyPaymentAcceptance> StartAsync(string orderId, long amount, string key, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using (var existing = Sql.Command(connection, transaction,
            "SELECT order_id, amount_cents FROM provider_requests WHERE idempotency_key = $key;", ("$key", key)))
        await using (var reader = await existing.ExecuteReaderAsync(token))
        {
            if (await reader.ReadAsync(token) && (reader.GetString(0) != orderId || reader.GetInt64(1) != amount))
                throw new IdempotencyConflictException("Provider idempotency key does not match order and amount.");
        }
        var id = $"pay_{orderId.Replace("-", "", StringComparison.Ordinal)}";
        await Sql.ExecuteAsync(connection, transaction, """
            INSERT INTO provider_payments(order_id, payment_id, status, created)
            VALUES($order, $id, 'Pending', 1) ON CONFLICT(order_id) DO NOTHING;
            INSERT INTO provider_requests(idempotency_key, order_id, amount_cents, payment_id)
            VALUES($key, $order, $amount, $id) ON CONFLICT(idempotency_key) DO NOTHING;
            """, token, ("$order", orderId), ("$id", id), ("$key", key), ("$amount", amount));
        var result = await ReadAsync(connection, transaction, orderId, token);
        await transaction.CommitAsync(token);
        return new(result.PaymentId!, result.Status);
    }
    public async Task<PaymentLookup> QueryAsync(Guid orderId, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        return await ReadAsync(connection, null, orderId.ToString(), token);
    }
    public async Task<PaymentLookup> CancelAsync(Guid orderId, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await Sql.ExecuteAsync(connection, transaction, """
            INSERT INTO provider_payments(order_id, payment_id, status, created)
            VALUES($order, $id, 'Cancelled', 0) ON CONFLICT(order_id) DO NOTHING;
            UPDATE provider_payments SET status = 'Cancelled' WHERE order_id = $order AND status = 'Pending';
            """, token, ("$order", orderId.ToString()), ("$id", $"pay_{orderId:N}"));
        var result = await ReadAsync(connection, transaction, orderId.ToString(), token);
        await transaction.CommitAsync(token);
        return result;
    }
    public async Task ObserveAsync(Guid orderId, string paymentId, ProviderStatus outcome, CancellationToken token)
    {
        if (outcome is not (ProviderStatus.Succeeded or ProviderStatus.Failed)) throw new ArgumentException("Invalid provider outcome.");
        await using var connection = await OpenAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var current = await ReadAsync(connection, transaction, orderId.ToString(), token);
        if (current.PaymentId != paymentId) throw new PaymentIdentityMismatchException();
        if (current.Status != ProviderStatus.Pending && current.Status != outcome)
            throw new InvalidStateTransitionException("provider", current.Status.ToString(), outcome.ToString());
        await Sql.ExecuteAsync(connection, transaction,
            "UPDATE provider_payments SET status = $status WHERE order_id = $order;", token,
            ("$status", outcome.ToString()), ("$order", orderId.ToString()));
        await transaction.CommitAsync(token);
    }
    private static async Task<PaymentLookup> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction,
        string orderId, CancellationToken token)
    {
        await using var command = Sql.Command(connection, transaction,
            "SELECT payment_id, status FROM provider_payments WHERE order_id = $order;", ("$order", orderId));
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new(reader.GetString(0), Enum.Parse<ProviderStatus>(reader.GetString(1))) : new(null, ProviderStatus.Unknown);
    }
}
public sealed class DurableLegacyPaymentSdk(PersistentPaymentSdk ledger) : ILegacyPaymentSdk
{
    public async void BeginPayment(string merchantReference, long amountCents, string idempotencyKey,
        Action<LegacyPaymentAcceptance> accepted, Action<Exception> rejected)
    {
        try { accepted(await ledger.StartAsync(merchantReference, amountCents, idempotencyKey, CancellationToken.None)); }
        catch (Exception exception) { rejected(exception); }
    }
    public Task<PaymentLookup> QueryAsync(Guid orderId, CancellationToken token) => ledger.QueryAsync(orderId, token);
    public Task<PaymentLookup> CancelAsync(Guid orderId, CancellationToken token) => ledger.CancelAsync(orderId, token);
    public Task ObserveAsync(Guid orderId, string paymentId, ProviderStatus outcome, CancellationToken token) => ledger.ObserveAsync(orderId, paymentId, outcome, token);
}
