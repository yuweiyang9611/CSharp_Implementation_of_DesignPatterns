using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;

namespace ReliableCheckout.Messaging;

public sealed class OutboxDispatcher(OutboxLeases leases, IEnumerable<IOutboxHandler> handlers,
    IFailureInjector failures, ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private readonly Dictionary<string, IOutboxHandler> handlersByType = handlers.ToDictionary(handler => handler.MessageType, StringComparer.Ordinal);
    public async Task<DispatchReport> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;
        var failed = 0;
        for (var i = 0; i < 20; i++)
        {
            var claimed = await leases.ClaimAsync(cancellationToken);
            if (claimed is null) break;
            using var ownership = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var renewal = RenewAsync(claimed, ownership);
            try
            {
                var message = claimed.Message;
                failures.ThrowIfScheduled($"outbox:{message.Type}");
                if (!handlersByType.TryGetValue(message.Type, out var handler))
                    throw new InvalidOperationException($"No handler for '{message.Type}'.");
                await handler.HandleAsync(message, ownership.Token);
                failures.ThrowIfScheduled($"outbox:after-handler:{message.Type}");
                if (await leases.CompleteAsync(claimed, cancellationToken)) processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                failed++;
                await leases.FailAsync(claimed, exception, cancellationToken);
                logger.LogWarning(exception, "Outbox event {EventId} failed on attempt {Attempt}", claimed.Message.Id, claimed.Message.Attempts);
            }
            finally
            {
                await ownership.CancelAsync();
                await renewal;
            }
        }
        return new(processed, failed);
    }
    private async Task RenewAsync(ClaimedMessage claimed, CancellationTokenSource ownership)
    {
        try
        {
            using var timer = new PeriodicTimer(leases.RenewalInterval);
            while (await timer.WaitForNextTickAsync(ownership.Token))
                if (!await leases.RenewAsync(claimed, ownership.Token)) { await ownership.CancelAsync(); break; }
        }
        catch (OperationCanceledException) when (ownership.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Lost lease for {EventId}", claimed.Message.Id);
            await ownership.CancelAsync();
        }
    }
}

public sealed class OutboxWorker(IOutboxDispatcher dispatcher, ReservationService reservations,
    IConfiguration configuration, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(50,
            configuration.GetValue("ReliableCheckout:OutboxPollingMilliseconds", 500))));
        try
        {
            do
            {
                try
                {
                    await reservations.ExpireAsync(stoppingToken);
                    var report = await dispatcher.DispatchBatchAsync(stoppingToken);
                    if (report.Processed > 0 || report.Failed > 0)
                        logger.LogInformation("Outbox batch: {Processed} processed, {Failed} failed", report.Processed, report.Failed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { logger.LogError(exception, "Unexpected outbox worker failure"); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
