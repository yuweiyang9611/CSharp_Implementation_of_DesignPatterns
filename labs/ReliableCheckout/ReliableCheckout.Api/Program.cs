using Microsoft.AspNetCore.Http.HttpResults;
using ReliableCheckout.Application;
using ReliableCheckout.Domain;
using ReliableCheckout.Infrastructure;
using ReliableCheckout.Messaging;
using ReliableCheckout.Payments;

var builder = WebApplication.CreateBuilder(args);

// JSON console logs keep named message-template fields queryable and avoid OS-specific sinks.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<DeterministicFailureInjector>();
builder.Services.AddSingleton<IFailureInjector>(services => services.GetRequiredService<DeterministicFailureInjector>());
builder.Services.AddSingleton<CheckoutDatabase>();
builder.Services.AddSingleton<CheckoutStore>();
builder.Services.AddSingleton<PaymentCallbackService>();

builder.Services.AddSingleton<InMemoryLegacyPaymentSdk>();
builder.Services.AddSingleton<ILegacyPaymentSdk>(services => services.GetRequiredService<InMemoryLegacyPaymentSdk>());
builder.Services.AddSingleton<IPaymentGateway, CallbackPaymentGatewayAdapter>();

builder.Services.AddSingleton<IOutboxHandler, PaymentRequestedHandler>();
builder.Services.AddSingleton<IOutboxHandler>(services => new OrderProjectionHandler(
    "OrderPaid",
    services.GetRequiredService<CheckoutDatabase>(),
    services.GetRequiredService<IClock>(),
    services.GetRequiredService<ILogger<OrderProjectionHandler>>()));
builder.Services.AddSingleton<IOutboxHandler>(services => new OrderProjectionHandler(
    "OrderPaymentFailed",
    services.GetRequiredService<CheckoutDatabase>(),
    services.GetRequiredService<IClock>(),
    services.GetRequiredService<ILogger<OrderProjectionHandler>>()));
builder.Services.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();
builder.Services.AddHostedService<OutboxWorker>();

var app = builder.Build();

var database = app.Services.GetRequiredService<CheckoutDatabase>();
await database.InitializeAsync(
    app.Configuration.GetValue("ReliableCheckout:SeedDemoInventory", true),
    app.Lifetime.ApplicationStopping);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/orders", async Task<Results<Created<OrderResponse>, Ok<OrderResponse>, BadRequest<ApiError>, Conflict<ApiError>>> (
    HttpRequest httpRequest,
    CreateOrderRequest request,
    CheckoutStore store,
    CancellationToken cancellationToken) =>
{
    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
    {
        return TypedResults.BadRequest(new ApiError(
            "invalid_idempotency_key",
            "Idempotency-Key is required and must be at most 128 characters."));
    }

    if (string.IsNullOrWhiteSpace(request.Sku) || request.Quantity <= 0)
    {
        return TypedResults.BadRequest(new ApiError(
            "invalid_order",
            "Sku is required and Quantity must be greater than zero."));
    }

    try
    {
        var result = await store.CreateOrderAsync(idempotencyKey, request, cancellationToken);
        var response = OrderResponse.From(result.Order, result.Replayed);
        return result.Replayed
            ? TypedResults.Ok(response)
            : TypedResults.Created($"/orders/{result.Order.Id}", response);
    }
    catch (IdempotencyConflictException exception)
    {
        return TypedResults.Conflict(new ApiError("idempotency_conflict", exception.Message));
    }
    catch (InsufficientStockException exception)
    {
        return TypedResults.Conflict(new ApiError("insufficient_stock", exception.Message));
    }
});

app.MapGet("/orders/{orderId:guid}", async Task<Results<Ok<OrderResponse>, NotFound<ApiError>>> (
    Guid orderId,
    CheckoutStore store,
    CancellationToken cancellationToken) =>
{
    var order = await store.GetOrderAsync(orderId, cancellationToken);
    return order is null
        ? TypedResults.NotFound(new ApiError("order_not_found", $"Order '{orderId}' was not found."))
        : TypedResults.Ok(OrderResponse.From(order));
});

app.MapPost("/payments/callback", async Task<Results<Ok<OrderResponse>, BadRequest<ApiError>, NotFound<ApiError>, Conflict<ApiError>>> (
    PaymentWebhookRequest request,
    PaymentCallbackService callbacks,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.EventId) ||
        string.IsNullOrWhiteSpace(request.ExternalPaymentId) ||
        string.IsNullOrWhiteSpace(request.Outcome))
    {
        return TypedResults.BadRequest(new ApiError(
            "invalid_webhook",
            "EventId, ExternalPaymentId, and Outcome are required."));
    }

    try
    {
        var result = await callbacks.ApplyAsync(request, cancellationToken);
        return TypedResults.Ok(OrderResponse.From(result.Order, result.Replayed));
    }
    catch (ArgumentException exception)
    {
        return TypedResults.BadRequest(new ApiError("invalid_webhook", exception.Message));
    }
    catch (OrderNotFoundException exception)
    {
        return TypedResults.NotFound(new ApiError("order_not_found", exception.Message));
    }
    catch (IdempotencyConflictException exception)
    {
        return TypedResults.Conflict(new ApiError("idempotency_conflict", exception.Message));
    }
    catch (PaymentIdentityMismatchException exception)
    {
        return TypedResults.Conflict(new ApiError("payment_identity_mismatch", exception.Message));
    }
    catch (InvalidStateTransitionException exception)
    {
        return TypedResults.Conflict(new ApiError("invalid_state_transition", exception.Message));
    }
});

app.MapGet("/inventory/{sku}", async (string sku, CheckoutStore store, CancellationToken cancellationToken) =>
{
    var available = await store.GetInventoryAsync(sku, cancellationToken);
    return available is null
        ? Results.NotFound(new ApiError("sku_not_found", $"SKU '{sku}' was not found."))
        : Results.Ok(new { sku = sku.ToUpperInvariant(), available });
});

app.Run();

public partial class Program;
