using Microsoft.Data.Sqlite;
using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;

namespace ReliableCheckout.Messaging;

public sealed class OutboxDispatcher(
    CheckoutDatabase database,
    IEnumerable<IOutboxHandler> handlers,
    IFailureInjector failureInjector,
    IClock clock,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private readonly IReadOnlyDictionary<string, IOutboxHandler> handlersByType = handlers.ToDictionary(
        handler => handler.MessageType,
        StringComparer.Ordinal);

    public async Task<DispatchReport> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        var messages = await ReadDueMessagesAsync(cancellationToken);
        var processed = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            try
            {
                if (!handlersByType.TryGetValue(message.Type, out var handler))
                {
                    throw new InvalidOperationException($"No outbox handler is registered for '{message.Type}'.");
                }

                failureInjector.ThrowIfScheduled($"outbox:{message.Type}");
                await handler.HandleAsync(message, cancellationToken);
                failureInjector.ThrowIfScheduled($"outbox:after-handler:{message.Type}");
                await MarkProcessedAsync(message.Id, cancellationToken);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                await MarkFailedAsync(message, exception, cancellationToken);
                logger.LogWarning(
                    exception,
                    "Outbox event {EventId} of type {MessageType} failed on attempt {Attempt}",
                    message.Id,
                    message.Type,
                    message.Attempts + 1);
            }
        }

        return new DispatchReport(processed, failed);
    }

    private async Task<IReadOnlyList<OutboxEnvelope>> ReadDueMessagesAsync(CancellationToken cancellationToken)
    {
        var results = new List<OutboxEnvelope>();
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, type, aggregate_id, payload, attempts, occurred_at
            FROM outbox
            WHERE processed_at IS NULL
              AND (next_attempt_at IS NULL OR next_attempt_at <= $now)
            ORDER BY occurred_at
            LIMIT 20;
            """;
        command.Parameters.AddWithValue("$now", CheckoutStore.FormatTimestamp(clock.UtcNow));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OutboxEnvelope(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                Guid.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.GetInt32(4),
                CheckoutStore.ParseTimestamp(reader.GetString(5))));
        }

        return results;
    }

    private async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE outbox
            SET processed_at = $now, last_error = NULL
            WHERE id = $id AND processed_at IS NULL;
            """;
        command.Parameters.AddWithValue("$now", CheckoutStore.FormatTimestamp(clock.UtcNow));
        command.Parameters.AddWithValue("$id", eventId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(
        OutboxEnvelope message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var attempt = message.Attempts + 1;
        var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 6)));
        var error = exception.Message.Length <= 1000 ? exception.Message : exception.Message[..1000];

        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE outbox
            SET attempts = $attempts, next_attempt_at = $nextAttempt, last_error = $error
            WHERE id = $id AND processed_at IS NULL;
            """;
        command.Parameters.AddWithValue("$attempts", attempt);
        command.Parameters.AddWithValue("$nextAttempt", CheckoutStore.FormatTimestamp(clock.UtcNow.Add(delay)));
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$id", message.Id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class OutboxWorker(
    IOutboxDispatcher dispatcher,
    IConfiguration configuration,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var milliseconds = Math.Max(
            50,
            configuration.GetValue("ReliableCheckout:OutboxPollingMilliseconds", 500));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(milliseconds));

        do
        {
            try
            {
                var report = await dispatcher.DispatchBatchAsync(stoppingToken);
                if (report.Processed > 0 || report.Failed > 0)
                {
                    logger.LogInformation(
                        "Outbox batch completed: {Processed} processed, {Failed} failed",
                        report.Processed,
                        report.Failed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected outbox worker failure");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
