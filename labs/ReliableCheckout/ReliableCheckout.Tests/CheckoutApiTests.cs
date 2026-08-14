namespace ReliableCheckout.Tests;

public sealed class CheckoutApiTests
{
    [Fact]
    public async Task Null_callback_outcome_returns_bad_request_instead_of_server_error()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/payments/callback",
            new
            {
                eventId = "missing-outcome",
                orderId = Guid.NewGuid(),
                externalPaymentId = "pay_unknown",
                outcome = (string?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_webhook", (await response.Content.ReadFromJsonAsync<ApiError>())?.Code);
    }

    [Fact]
    public async Task Duplicate_submission_returns_same_order_and_reserves_inventory_once()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();
        await factory.Store.SetInventoryAsync("KBD-01", available: 5, unitPriceCents: 12_500);

        var first = await PostOrderAsync(client, "checkout-001", "KBD-01", quantity: 2);
        var second = await PostOrderAsync(client, "checkout-001", "KBD-01", quantity: 2);
        var conflicting = await PostOrderAsync(client, "checkout-001", "KBD-01", quantity: 1);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        var created = await ReadOrderAsync(first);
        var replayed = await ReadOrderAsync(second);
        Assert.Equal(created.Id, replayed.Id);
        Assert.False(created.Replayed);
        Assert.True(replayed.Replayed);
        Assert.Equal(3, await factory.Store.GetInventoryAsync("KBD-01"));
    }

    [Fact]
    public async Task Concurrent_buyers_competing_for_last_unit_have_exactly_one_winner()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();
        await factory.Store.SetInventoryAsync("LAST-ONE", available: 1, unitPriceCents: 5_000);

        var responses = await Task.WhenAll(
            PostOrderAsync(client, "buyer-a", "LAST-ONE", quantity: 1),
            PostOrderAsync(client, "buyer-b", "LAST-ONE", quantity: 1));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, await factory.Store.GetInventoryAsync("LAST-ONE"));
    }

    [Fact]
    public async Task Duplicate_and_out_of_order_callbacks_cannot_corrupt_paid_order()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();
        await factory.Store.SetInventoryAsync("CAM-01", available: 1);
        var creation = await PostOrderAsync(client, "camera-order", "CAM-01", quantity: 1);
        var order = await ReadOrderAsync(creation);

        var early = await PostCallbackAsync(
            client,
            eventId: "callback-early",
            order.Id,
            externalPaymentId: "not-created-yet",
            outcome: "succeeded");
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("invalid_state_transition", (await early.Content.ReadFromJsonAsync<ApiError>())?.Code);

        var dispatch = await factory.Dispatcher.DispatchBatchAsync();
        Assert.Equal(new DispatchReport(Processed: 1, Failed: 0), dispatch);

        var awaitingResult = await client.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}");
        Assert.NotNull(awaitingResult);
        Assert.Equal("Requested", awaitingResult.PaymentStatus);
        Assert.NotNull(awaitingResult.ExternalPaymentId);

        var succeeded = await PostCallbackAsync(
            client,
            eventId: "callback-success",
            order.Id,
            awaitingResult.ExternalPaymentId,
            outcome: "succeeded");
        var duplicate = await PostCallbackAsync(
            client,
            eventId: "callback-success",
            order.Id,
            awaitingResult.ExternalPaymentId,
            outcome: "succeeded");
        var staleFailure = await PostCallbackAsync(
            client,
            eventId: "callback-stale-failure",
            order.Id,
            awaitingResult.ExternalPaymentId,
            outcome: "failed");

        Assert.Equal(HttpStatusCode.OK, succeeded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.True((await ReadOrderAsync(duplicate)).Replayed);
        Assert.Equal(HttpStatusCode.Conflict, staleFailure.StatusCode);

        var finalOrder = await client.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}");
        Assert.NotNull(finalOrder);
        Assert.Equal("Paid", finalOrder.Status);
        Assert.Equal("Succeeded", finalOrder.PaymentStatus);
    }

    [Fact]
    public async Task First_outbox_delivery_failure_is_retried_after_clock_advances()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();
        await factory.Store.SetInventoryAsync("MOUSE-01", available: 2);
        var creation = await PostOrderAsync(client, "mouse-order", "MOUSE-01", quantity: 1);
        var order = await ReadOrderAsync(creation);
        factory.Failures.FailNext("outbox:PaymentRequested");

        var failed = await factory.Dispatcher.DispatchBatchAsync();
        var afterFailure = Assert.Single(await factory.Store.GetOutboxAsync());

        Assert.Equal(new DispatchReport(Processed: 0, Failed: 1), failed);
        Assert.Equal(1, afterFailure.Attempts);
        Assert.Null(afterFailure.ProcessedAt);
        Assert.NotNull(afterFailure.LastError);
        Assert.Equal(0, factory.LegacyPaymentSdk.RequestCount);

        factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var recovered = await factory.Dispatcher.DispatchBatchAsync();

        Assert.Equal(new DispatchReport(Processed: 1, Failed: 0), recovered);
        Assert.Equal(1, factory.LegacyPaymentSdk.RequestCount);
        var refreshed = await client.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}");
        Assert.NotNull(refreshed);
        Assert.Equal("Requested", refreshed.PaymentStatus);
    }

    [Fact]
    public async Task Consumer_replay_after_post_handler_crash_does_not_request_payment_twice()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();
        await factory.Store.SetInventoryAsync("HEADSET-01", available: 1);
        await PostOrderAsync(client, "headset-order", "HEADSET-01", quantity: 1);
        factory.Failures.FailNext("outbox:after-handler:PaymentRequested");

        var crashWindow = await factory.Dispatcher.DispatchBatchAsync();
        Assert.Equal(new DispatchReport(Processed: 0, Failed: 1), crashWindow);
        Assert.Equal(1, factory.LegacyPaymentSdk.RequestCount);

        factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var replay = await factory.Dispatcher.DispatchBatchAsync();

        Assert.Equal(new DispatchReport(Processed: 1, Failed: 0), replay);
        Assert.Equal(1, factory.LegacyPaymentSdk.RequestCount);
        Assert.NotNull(Assert.Single(await factory.Store.GetOutboxAsync()).ProcessedAt);
    }

    private static Task<HttpResponseMessage> PostOrderAsync(
        HttpClient client,
        string idempotencyKey,
        string sku,
        int quantity)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(sku, quantity))
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(message);
    }

    private static Task<HttpResponseMessage> PostCallbackAsync(
        HttpClient client,
        string eventId,
        Guid orderId,
        string externalPaymentId,
        string outcome) => client.PostAsJsonAsync(
            "/payments/callback",
            new PaymentWebhookRequest(eventId, orderId, externalPaymentId, outcome));

    private static async Task<OrderResponse> ReadOrderAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<OrderResponse>()
        ?? throw new InvalidOperationException("Response did not contain an order.");
}
