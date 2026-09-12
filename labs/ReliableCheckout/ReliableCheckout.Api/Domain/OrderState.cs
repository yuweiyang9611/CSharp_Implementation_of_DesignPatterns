namespace ReliableCheckout.Domain;

public enum OrderStatus
{
    AwaitingPayment,
    Paid,
    PaymentFailed,
    CancellationPending,
    Cancelled
}

public enum PaymentStatus
{
    PendingRequest,
    Requested,
    Succeeded,
    Failed,
    Cancelled
}

public enum PaymentSignal
{
    RequestAccepted,
    Succeeded,
    Failed,
    Cancelled
}

public sealed class InvalidStateTransitionException(string machine, string current, string signal)
    : InvalidOperationException($"{machine} cannot apply '{signal}' while in '{current}'.")
{
    public string Machine { get; } = machine;

    public string Current { get; } = current;

    public string Signal { get; } = signal;
}

/// <summary>
/// Keeps payment transition rules in one place. Adding a new provider does not duplicate them.
/// </summary>
public static class PaymentStateMachine
{
    public static PaymentStatus Apply(PaymentStatus current, PaymentSignal signal) => (current, signal) switch
    {
        (PaymentStatus.PendingRequest, PaymentSignal.RequestAccepted) => PaymentStatus.Requested,
        (PaymentStatus.Requested, PaymentSignal.Succeeded) => PaymentStatus.Succeeded,
        (PaymentStatus.Requested, PaymentSignal.Failed) => PaymentStatus.Failed,
        (PaymentStatus.PendingRequest or PaymentStatus.Requested, PaymentSignal.Cancelled) => PaymentStatus.Cancelled,
        (PaymentStatus.PendingRequest, PaymentSignal.Succeeded) => PaymentStatus.Succeeded,
        (PaymentStatus.Succeeded, PaymentSignal.Succeeded) => current,
        (PaymentStatus.Failed, PaymentSignal.Failed) => current,
        (PaymentStatus.Cancelled, PaymentSignal.Cancelled) => current,
        _ => throw new InvalidStateTransitionException("payment", current.ToString(), signal.ToString())
    };
}

/// <summary>
/// Order state is derived from accepted payment transitions, never directly from webhook text.
/// </summary>
public static class OrderStateMachine
{
    public static OrderStatus ApplyPaymentResult(OrderStatus current, PaymentSignal signal) => (current, signal) switch
    {
        (OrderStatus.AwaitingPayment or OrderStatus.CancellationPending, PaymentSignal.Succeeded) => OrderStatus.Paid,
        (OrderStatus.AwaitingPayment or OrderStatus.CancellationPending, PaymentSignal.Failed) => OrderStatus.PaymentFailed,
        (OrderStatus.AwaitingPayment or OrderStatus.CancellationPending, PaymentSignal.Cancelled) => OrderStatus.Cancelled,
        (OrderStatus.Paid, PaymentSignal.Succeeded) => current,
        (OrderStatus.PaymentFailed, PaymentSignal.Failed) => current,
        (OrderStatus.Cancelled, PaymentSignal.Cancelled) => current,
        _ => throw new InvalidStateTransitionException("order", current.ToString(), signal.ToString())
    };
}
