using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;

namespace ReliableCheckout.Messaging;

public sealed class OutboxLeases(CheckoutDatabase database, IClock clock, IConfiguration configuration)
{
    public TimeSpan Duration { get; } = TimeSpan.FromSeconds(configuration.GetValue("ReliableCheckout:LeaseSeconds", 30d));
    public TimeSpan RenewalInterval { get; } = TimeSpan.FromSeconds(configuration.GetValue("ReliableCheckout:LeaseRenewalSeconds", 10d));
    public int MaximumAttempts { get; } = configuration.GetValue("ReliableCheckout:MaximumAttempts", 5);

    public async Task<ClaimedMessage?> ClaimAsync(CancellationToken token = default)
    {
        if (Duration <= TimeSpan.Zero || RenewalInterval <= TimeSpan.Zero || RenewalInterval >= Duration || MaximumAttempts < 1)
            throw new InvalidOperationException("Lease renewal must be positive and shorter than the lease; MaximumAttempts must be positive.");
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var now = CheckoutStore.FormatTimestamp(clock.UtcNow);
        await Sql.ExecuteAsync(connection, transaction, """
            UPDATE outbox SET dead_letter_at = $now, lease_token = NULL, lease_until = NULL,
                last_error = COALESCE(last_error, 'Lease expired on final delivery attempt')
            WHERE processed_at IS NULL AND dead_letter_at IS NULL AND attempts >= $maximum
                AND (lease_until IS NULL OR lease_until <= $now);
            """, token, ("$now", now), ("$maximum", MaximumAttempts));
        OutboxEnvelope? message = null;
        await using (var command = Sql.Command(connection, transaction, """
            SELECT id, type, aggregate_id, payload, attempts, occurred_at FROM outbox
            WHERE processed_at IS NULL AND dead_letter_at IS NULL AND attempts < $maximum
                AND (lease_until IS NULL OR lease_until <= $now)
                AND (next_attempt_at IS NULL OR next_attempt_at <= $now)
            ORDER BY occurred_at, id LIMIT 1;
            """, ("$now", now), ("$maximum", MaximumAttempts)))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (await reader.ReadAsync(token))
                message = new(Guid.Parse(reader.GetString(0)), reader.GetString(1), Guid.Parse(reader.GetString(2)),
                    reader.GetString(3), reader.GetInt32(4) + 1, CheckoutStore.ParseTimestamp(reader.GetString(5)));
        }
        if (message is null) { await transaction.CommitAsync(token); return null; }
        var lease = Guid.NewGuid().ToString("N");
        await Sql.ExecuteAsync(connection, transaction, """
            UPDATE outbox SET lease_token = $lease, lease_until = $until,
                attempts = attempts + 1, total_attempts = total_attempts + 1 WHERE id = $id;
            """, token, ("$id", message.Id.ToString()), ("$lease", lease), ("$until", CheckoutStore.FormatTimestamp(clock.UtcNow.Add(Duration))));
        await transaction.CommitAsync(token);
        return new(message, lease);
    }

    private async Task<bool> UpdateOwnedAsync(ClaimedMessage claimed, string assignments, CancellationToken token,
        params (string Name, object? Value)[] values)
    {
        await using var connection = await database.OpenConnectionAsync(token);
        var parameters = values.Concat(new (string, object?)[] {
            ("$id", claimed.Message.Id.ToString()), ("$lease", claimed.LeaseToken), ("$now", CheckoutStore.FormatTimestamp(clock.UtcNow)) }).ToArray();
        return await Sql.ExecuteAsync(connection, null,
            $"UPDATE outbox SET {assignments} WHERE id = $id AND lease_token = $lease AND lease_until > $now AND processed_at IS NULL AND dead_letter_at IS NULL;",
            token, parameters) == 1;
    }
    public Task<bool> RenewAsync(ClaimedMessage claimed, CancellationToken token = default) =>
        UpdateOwnedAsync(claimed, "lease_until = $until", token, ("$until", CheckoutStore.FormatTimestamp(clock.UtcNow.Add(Duration))));
    public Task<bool> CompleteAsync(ClaimedMessage claimed, CancellationToken token = default) =>
        UpdateOwnedAsync(claimed, "processed_at = $now, last_error = NULL, lease_token = NULL, lease_until = NULL", token);
    public Task<bool> FailAsync(ClaimedMessage claimed, Exception exception, CancellationToken token = default)
    {
        var exhausted = claimed.Message.Attempts >= MaximumAttempts;
        var error = exception.Message[..Math.Min(1000, exception.Message.Length)];
        return UpdateOwnedAsync(claimed,
            "last_error = $error, next_attempt_at = $next, dead_letter_at = $dead, lease_token = NULL, lease_until = NULL", token,
            ("$error", error), ("$next", exhausted ? null : CheckoutStore.FormatTimestamp(clock.UtcNow.AddSeconds(Math.Pow(2, Math.Min(claimed.Message.Attempts, 6))))),
            ("$dead", exhausted ? CheckoutStore.FormatTimestamp(clock.UtcNow) : null));
    }
    public async Task<bool> ReplayAsync(Guid id, CancellationToken token = default)
    {
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var count = await Sql.ExecuteAsync(connection, transaction, """
            INSERT INTO outbox_replays(event_id, replayed_at, attempts, last_error)
            SELECT id, $now, attempts, last_error FROM outbox WHERE id = $id AND dead_letter_at IS NOT NULL AND processed_at IS NULL;
            """, token, ("$id", id.ToString()), ("$now", CheckoutStore.FormatTimestamp(clock.UtcNow)));
        if (count == 1)
            await Sql.ExecuteAsync(connection, transaction, """
                UPDATE outbox SET attempts = 0, dead_letter_at = NULL, next_attempt_at = NULL,
                    lease_token = NULL, lease_until = NULL WHERE id = $id;
                """, token, ("$id", id.ToString()));
        await transaction.CommitAsync(token);
        return count == 1;
    }
}
