using System.Collections.Concurrent;

namespace ReliableCheckout.Payments;

public sealed record LegacyPaymentAcceptance(string PaymentId);

public interface ILegacyPaymentSdk
{
    void BeginPayment(
        string merchantReference,
        long amountCents,
        string idempotencyKey,
        Action<LegacyPaymentAcceptance> accepted,
        Action<Exception> rejected);
}

/// <summary>
/// Represents a vendor SDK whose callback API cannot be changed.
/// It also honors the supplied idempotency key, as a real payment provider should.
/// </summary>
public sealed class InMemoryLegacyPaymentSdk : ILegacyPaymentSdk
{
    private readonly ConcurrentDictionary<string, LegacyPaymentAcceptance> acceptances = new(StringComparer.Ordinal);
    private int requestCount;

    public int RequestCount => Volatile.Read(ref requestCount);

    public void BeginPayment(
        string merchantReference,
        long amountCents,
        string idempotencyKey,
        Action<LegacyPaymentAcceptance> accepted,
        Action<Exception> rejected)
    {
        try
        {
            if (!acceptances.TryGetValue(idempotencyKey, out var result))
            {
                var candidate = new LegacyPaymentAcceptance(
                    $"pay_{merchantReference.Replace("-", string.Empty, StringComparison.Ordinal)}");
                if (acceptances.TryAdd(idempotencyKey, candidate))
                {
                    Interlocked.Increment(ref requestCount);
                    result = candidate;
                }
                else
                {
                    result = acceptances[idempotencyKey];
                }
            }

            accepted(result);
        }
        catch (Exception exception)
        {
            rejected(exception);
        }
    }
}

public sealed record PaymentStartResult(string ExternalPaymentId);

public interface IPaymentGateway
{
    Task<PaymentStartResult> StartAsync(
        Guid orderId,
        long amountCents,
        Guid idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Adapter: translates a callback-style provider SDK into the application's Task-based port.
/// </summary>
public sealed class CallbackPaymentGatewayAdapter(ILegacyPaymentSdk sdk) : IPaymentGateway
{
    public async Task<PaymentStartResult> StartAsync(
        Guid orderId,
        long amountCents,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<PaymentStartResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        sdk.BeginPayment(
            orderId.ToString(),
            amountCents,
            idempotencyKey.ToString(),
            result => completion.TrySetResult(new PaymentStartResult(result.PaymentId)),
            exception => completion.TrySetException(exception));

        return await completion.Task;
    }
}
