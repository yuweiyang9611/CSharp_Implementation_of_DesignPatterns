namespace ReliableCheckout.Payments;

public enum ProviderStatus { Unknown, Pending, Succeeded, Failed, Cancelled }
public sealed record PaymentLookup(string? PaymentId, ProviderStatus Status);
public sealed record LegacyPaymentAcceptance(string PaymentId, ProviderStatus Status = ProviderStatus.Pending);
public interface ILegacyPaymentSdk
{
    void BeginPayment(string merchantReference, long amountCents, string idempotencyKey,
        Action<LegacyPaymentAcceptance> accepted, Action<Exception> rejected);
    Task<PaymentLookup> QueryAsync(Guid orderId, CancellationToken token);
    Task<PaymentLookup> CancelAsync(Guid orderId, CancellationToken token);
    Task ObserveAsync(Guid orderId, string paymentId, ProviderStatus outcome, CancellationToken token);
}
public sealed record PaymentStartResult(string ExternalPaymentId, ProviderStatus Status = ProviderStatus.Pending);
public interface IPaymentGateway
{
    Task<PaymentStartResult> StartAsync(Guid orderId, long amountCents, Guid idempotencyKey, CancellationToken cancellationToken);
    Task<PaymentLookup> QueryAsync(Guid orderId, CancellationToken token);
    Task<PaymentLookup> CancelAsync(Guid orderId, CancellationToken token);
    // Teaching provider control: models the terminal event originating at the simulated provider.
    Task ObserveAsync(Guid orderId, string paymentId, ProviderStatus outcome, CancellationToken token);
}
/// <summary>Adapts the legacy callback API to awaitable application operations.</summary>
public sealed class CallbackPaymentGatewayAdapter(ILegacyPaymentSdk sdk) : IPaymentGateway
{
    public async Task<PaymentStartResult> StartAsync(Guid orderId, long amountCents, Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<PaymentStartResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        sdk.BeginPayment(orderId.ToString(), amountCents, idempotencyKey.ToString(),
            result => completion.TrySetResult(new(result.PaymentId, result.Status)),
            exception => completion.TrySetException(exception));
        return await completion.Task;
    }
    public Task<PaymentLookup> QueryAsync(Guid orderId, CancellationToken token) => sdk.QueryAsync(orderId, token);
    public Task<PaymentLookup> CancelAsync(Guid orderId, CancellationToken token) => sdk.CancelAsync(orderId, token);
    public Task ObserveAsync(Guid orderId, string paymentId, ProviderStatus outcome, CancellationToken token) => sdk.ObserveAsync(orderId, paymentId, outcome, token);
}
