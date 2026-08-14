using Microsoft.Data.Sqlite;

namespace ReliableCheckout.Infrastructure;

public sealed class CheckoutDatabase
{
    public CheckoutDatabase(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration.GetConnectionString("Checkout")
            ?? throw new InvalidOperationException("Connection string 'Checkout' is required.");
        var builder = new SqliteConnectionStringBuilder(configured);
        if (!string.IsNullOrWhiteSpace(builder.DataSource)
            && builder.DataSource != ":memory:"
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(environment.ContentRootPath, builder.DataSource);
        }

        ConnectionString = builder.ToString();
    }

    public string ConnectionString { get; }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        EnsureDatabaseDirectoryExists();
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public async Task InitializeAsync(bool seedDemoInventory, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS inventory (
                sku TEXT PRIMARY KEY,
                available INTEGER NOT NULL CHECK (available >= 0),
                unit_price_cents INTEGER NOT NULL CHECK (unit_price_cents > 0)
            );

            CREATE TABLE IF NOT EXISTS orders (
                id TEXT PRIMARY KEY,
                idempotency_key TEXT NOT NULL UNIQUE,
                request_fingerprint TEXT NOT NULL,
                sku TEXT NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0),
                unit_price_cents INTEGER NOT NULL,
                total_cents INTEGER NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS payments (
                order_id TEXT PRIMARY KEY REFERENCES orders(id),
                external_payment_id TEXT UNIQUE,
                status TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS outbox (
                id TEXT PRIMARY KEY,
                type TEXT NOT NULL,
                aggregate_id TEXT NOT NULL,
                payload TEXT NOT NULL,
                occurred_at TEXT NOT NULL,
                attempts INTEGER NOT NULL DEFAULT 0,
                next_attempt_at TEXT,
                processed_at TEXT,
                last_error TEXT
            );

            CREATE INDEX IF NOT EXISTS ix_outbox_pending
                ON outbox(processed_at, next_attempt_at, occurred_at);

            CREATE TABLE IF NOT EXISTS consumer_receipts (
                consumer TEXT NOT NULL,
                event_id TEXT NOT NULL,
                fingerprint TEXT NOT NULL,
                processed_at TEXT NOT NULL,
                PRIMARY KEY (consumer, event_id)
            );

            CREATE TABLE IF NOT EXISTS order_projection (
                order_id TEXT PRIMARY KEY,
                status TEXT NOT NULL,
                source_event_id TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        if (seedDemoInventory)
        {
            await using var seed = connection.CreateCommand();
            seed.CommandText = """
                INSERT INTO inventory(sku, available, unit_price_cents)
                VALUES ('DEMO-SKU', 10, 1999)
                ON CONFLICT(sku) DO NOTHING;
                """;
            await seed.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private void EnsureDatabaseDirectoryExists()
    {
        var builder = new SqliteConnectionStringBuilder(ConnectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return;
        }

        var fullPath = Path.GetFullPath(builder.DataSource);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}
