using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReliableCheckout.Domain;

namespace ReliableCheckout.Tests;

public sealed class ReliabilityTests
{
    internal static async Task<OrderSnapshot> CreateAsync(ReliableCheckoutApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await factory.Store.SetInventoryAsync("TEST", 5);
        return (await factory.Store.CreateOrderAsync(Guid.NewGuid().ToString(), new("TEST", 2))).Order;
    }

    [Fact]
    public async Task Failed_callbacks_release_stock_only_once()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        var order = await CreateAsync(factory);
        await factory.Dispatcher.DispatchBatchAsync();
        order = (await factory.Store.GetOrderAsync(order.Id))!;
        var callbacks = factory.Services.GetRequiredService<PaymentCallbackService>();
        var request = new PaymentWebhookRequest("failed-1", order.Id, order.ExternalPaymentId!, "failed");
        await callbacks.ApplyAsync(request);
        Assert.True((await callbacks.ApplyAsync(request)).Replayed);
        await callbacks.ApplyAsync(request with { EventId = "failed-2" });
        Assert.Equal(5, await factory.Store.GetInventoryAsync("TEST"));
        Assert.Equal("Released", (await factory.Store.GetOrderAsync(order.Id))!.ReservationState);
    }

    [Fact]
    public async Task Committed_callback_replay_does_not_require_the_legacy_provider_ledger()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        var order = await CreateAsync(factory);
        await factory.Dispatcher.DispatchBatchAsync();
        order = (await factory.Store.GetOrderAsync(order.Id))!;
        var callbacks = factory.Services.GetRequiredService<PaymentCallbackService>();
        var request = new PaymentWebhookRequest("legacy-result", order.Id, order.ExternalPaymentId!, "failed");
        await callbacks.ApplyAsync(request);
        await using (var provider = new SqliteConnection(factory.LegacyPaymentSdk.ConnectionString))
        {
            await provider.OpenAsync();
            await using var clear = provider.CreateCommand();
            clear.CommandText = "DELETE FROM provider_requests; DELETE FROM provider_payments;";
            await clear.ExecuteNonQueryAsync();
        }
        Assert.True((await callbacks.ApplyAsync(request)).Replayed);
        Assert.Equal(5, await factory.Store.GetInventoryAsync("TEST"));
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => callbacks.ApplyAsync(request with { Outcome = "succeeded" }));
        Assert.Equal(0, factory.LegacyPaymentSdk.RequestCount);
    }

    [Fact]
    public async Task Timeout_cancels_before_releasing_and_delayed_create_cannot_resurrect_payment()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        var order = await CreateAsync(factory);
        var reservations = factory.Services.GetRequiredService<ReservationService>();
        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(1, await reservations.ExpireAsync());
        Assert.Equal(0, await reservations.ExpireAsync());
        Assert.Equal(3, await factory.Store.GetInventoryAsync("TEST"));
        var cancelled = await factory.LegacyPaymentSdk.CancelAsync(order.Id, default);
        await reservations.ApplyProviderResultAsync(order.Id, cancelled, default);
        await factory.Dispatcher.DispatchBatchAsync();
        var final = (await factory.Store.GetOrderAsync(order.Id))!;
        Assert.Equal(OrderStatus.Cancelled, final.Status);
        Assert.Equal("Released", final.ReservationState);
        Assert.Equal(5, await factory.Store.GetInventoryAsync("TEST"));
        Assert.Equal(0, factory.LegacyPaymentSdk.RequestCount);
    }

    [Fact]
    public async Task Unknown_provider_keeps_reservation_and_dead_letters_cancellation()
    {
        using var factory = new ReliableCheckoutApplicationFactory
        {
            ConfigureServices = services => services.AddSingleton<IPaymentGateway, UnknownGateway>()
        };
        var order = await CreateAsync(factory);
        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        await factory.Services.GetRequiredService<ReservationService>().ExpireAsync();
        for (var i = 0; i < 5; i++) { await factory.Dispatcher.DispatchBatchAsync(); factory.Clock.Advance(TimeSpan.FromSeconds(64)); }
        var final = (await factory.Store.GetOrderAsync(order.Id))!;
        Assert.Equal(OrderStatus.CancellationPending, final.Status);
        Assert.Equal("Held", final.ReservationState);
        Assert.Equal(3, await factory.Store.GetInventoryAsync("TEST"));
        Assert.NotNull((await factory.Store.GetOutboxAsync()).Single(item => item.Type == "CancelPayment").DeadLetterAt);
    }

    [Fact]
    public async Task Provider_success_wins_cancellation_and_consumes_stock()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        var order = await CreateAsync(factory);
        await factory.Dispatcher.DispatchBatchAsync();
        order = (await factory.Store.GetOrderAsync(order.Id))!;
        await factory.LegacyPaymentSdk.ObserveAsync(order.Id, order.ExternalPaymentId!, ProviderStatus.Succeeded, default);
        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        await factory.Services.GetRequiredService<ReservationService>().ExpireAsync();
        await factory.Dispatcher.DispatchBatchAsync();
        Assert.Equal(OrderStatus.Paid, (await factory.Store.GetOrderAsync(order.Id))!.Status);
        Assert.Equal("Consumed", (await factory.Store.GetOrderAsync(order.Id))!.ReservationState);
        Assert.Equal(3, await factory.Store.GetInventoryAsync("TEST"));
    }

    [Fact]
    public async Task Durable_provider_arbitrates_simultaneous_success_and_cancellation()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        var order = await CreateAsync(factory);
        var accepted = await factory.LegacyPaymentSdk.StartAsync(order.Id.ToString(), order.TotalCents, "provider-key", default);
        var successes = Task.Run(async () =>
        {
            try { await factory.LegacyPaymentSdk.ObserveAsync(order.Id, accepted.PaymentId, ProviderStatus.Succeeded, default); }
            catch (InvalidStateTransitionException) { }
        });
        await Task.WhenAll(successes, Task.Run(() => factory.LegacyPaymentSdk.CancelAsync(order.Id, default)));
        var result = await factory.LegacyPaymentSdk.QueryAsync(order.Id, default);
        Assert.Contains(result.Status, new[] { ProviderStatus.Succeeded, ProviderStatus.Cancelled });
        Assert.Equal(result, await factory.LegacyPaymentSdk.CancelAsync(order.Id, default));
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => factory.LegacyPaymentSdk.StartAsync(order.Id.ToString(), 1, "provider-key", default));
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => factory.LegacyPaymentSdk.StartAsync(Guid.NewGuid().ToString(), order.TotalCents, "provider-key", default));
    }

    [Fact]
    public async Task Leases_exclude_competitors_and_reject_stale_owner_after_takeover()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        await CreateAsync(factory);
        var leases = factory.Services.GetRequiredService<OutboxLeases>();
        var first = (await leases.ClaimAsync())!;
        Assert.Null(await leases.ClaimAsync());
        factory.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.True(await leases.RenewAsync(first));
        factory.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Null(await leases.ClaimAsync());
        factory.Clock.Advance(TimeSpan.FromSeconds(11));
        var second = (await leases.ClaimAsync())!;
        Assert.Equal(first.Message.Id, second.Message.Id);
        Assert.NotEqual(first.LeaseToken, second.LeaseToken);
        Assert.False(await leases.RenewAsync(first));
        Assert.False(await leases.CompleteAsync(first));
        Assert.False(await leases.FailAsync(first, new Exception("stale")));
        Assert.True(await leases.CompleteAsync(second));
    }

    [Fact]
    public async Task Dead_letter_replay_preserves_identity_history_and_total_attempts()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        await CreateAsync(factory);
        var leases = factory.Services.GetRequiredService<OutboxLeases>();
        Guid id = default;
        for (var i = 0; i < 5; i++)
        {
            var claim = (await leases.ClaimAsync())!;
            id = claim.Message.Id;
            Assert.True(await leases.FailAsync(claim, new Exception("unavailable")));
            factory.Clock.Advance(TimeSpan.FromSeconds(64));
        }
        Assert.Null(await leases.ClaimAsync());
        Assert.NotNull(Assert.Single(await factory.Store.GetOutboxAsync()).DeadLetterAt);
        Assert.True(await leases.ReplayAsync(id));
        Assert.False(await leases.ReplayAsync(id));
        var replay = (await leases.ClaimAsync())!;
        Assert.Equal(id, replay.Message.Id);
        Assert.Equal(1, replay.Message.Attempts);
        Assert.Equal(6, Assert.Single(await factory.Store.GetOutboxAsync()).TotalAttempts);
        Assert.Equal(1L, await ScalarAsync(factory, "SELECT COUNT(*) FROM outbox_replays;"));
    }

    [Fact]
    public async Task Final_attempt_crash_expires_to_dead_letter()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        factory.Settings["ReliableCheckout:MaximumAttempts"] = "1";
        await CreateAsync(factory);
        var leases = factory.Services.GetRequiredService<OutboxLeases>();
        Assert.NotNull(await leases.ClaimAsync());
        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Null(await leases.ClaimAsync());
        Assert.NotNull(Assert.Single(await factory.Store.GetOutboxAsync()).DeadLetterAt);
    }

    [Fact]
    public async Task Background_renewal_keeps_ownership_during_slow_handler()
    {
        var slow = new SlowHandler();
        using var factory = new ReliableCheckoutApplicationFactory
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IClock>(); services.AddSingleton<IClock, SystemClock>();
                services.RemoveAll<IOutboxHandler>(); services.AddSingleton<IOutboxHandler>(slow);
            }
        };
        factory.Settings["ReliableCheckout:LeaseSeconds"] = "0.6";
        factory.Settings["ReliableCheckout:LeaseRenewalSeconds"] = "0.1";
        await CreateAsync(factory);
        var dispatch = factory.Dispatcher.DispatchBatchAsync();
        await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(1100);
        Assert.Null(await factory.Services.GetRequiredService<OutboxLeases>().ClaimAsync());
        slow.Release.SetResult();
        Assert.Equal(1, (await dispatch).Processed);
    }

    [Fact]
    public async Task Legacy_database_upgrade_compensates_failed_orders_exactly_once()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        await CreateAsync(factory);
        await factory.Dispatcher.DispatchBatchAsync();
        await ScalarAsync(factory, """
            UPDATE orders SET status = 'PaymentFailed'; UPDATE payments SET status = 'Failed';
            DROP TABLE reservations; DROP TABLE outbox_replays;
            ALTER TABLE outbox DROP COLUMN lease_token;
            ALTER TABLE outbox DROP COLUMN lease_until;
            ALTER TABLE outbox DROP COLUMN total_attempts;
            ALTER TABLE outbox DROP COLUMN dead_letter_at;
            PRAGMA user_version = 0;
            """);
        var database = factory.Services.GetRequiredService<CheckoutDatabase>();
        await database.InitializeAsync(false);
        await database.InitializeAsync(false);
        Assert.Equal(5, await factory.Store.GetInventoryAsync("TEST"));
        Assert.Equal("Released", await ScalarAsync(factory, "SELECT state FROM reservations;"));
        Assert.Equal(1L, await ScalarAsync(factory, "SELECT COUNT(*) FROM consumer_receipts;"));
        Assert.Single(await factory.Store.GetOutboxAsync());
        Assert.Equal(1L, await ScalarAsync(factory, "PRAGMA user_version;"));
    }

    internal static async Task<object?> ScalarAsync(ReliableCheckoutApplicationFactory factory, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
    private sealed class SlowHandler : IOutboxHandler
    {
        public string MessageType => "PaymentRequested";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
        { Entered.SetResult(); await Release.Task.WaitAsync(cancellationToken); }
    }
    private sealed class UnknownGateway : IPaymentGateway
    {
        public Task<PaymentStartResult> StartAsync(Guid orderId, long amountCents, Guid idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(new PaymentStartResult("unknown"));
        public Task<PaymentLookup> QueryAsync(Guid orderId, CancellationToken token) => Task.FromResult(new PaymentLookup(null, ProviderStatus.Unknown));
        public Task<PaymentLookup> CancelAsync(Guid orderId, CancellationToken token) => QueryAsync(orderId, token);
        public Task ObserveAsync(Guid orderId, string paymentId, ProviderStatus outcome, CancellationToken token) => Task.CompletedTask;
    }
}
