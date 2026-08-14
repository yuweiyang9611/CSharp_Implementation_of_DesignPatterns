using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.States;

public sealed class AwaitingPaymentState : OrderStateBase
{
    public static AwaitingPaymentState Instance { get; } = new();

    private AwaitingPaymentState()
    {
    }

    public override OrderStatus Status => OrderStatus.AwaitingPayment;

    public override void Pay(Order order, string paymentReference)
    {
        order.RecordPayment(paymentReference);
        order.TransitionTo(PaidState.Instance, $"支付成功，流水号 {paymentReference}。");
    }

    public override void Cancel(Order order, string reason)
    {
        order.TransitionTo(CancelledState.Instance, reason);
    }
}

public sealed class PaidState : OrderStateBase
{
    public static PaidState Instance { get; } = new();

    private PaidState()
    {
    }

    public override OrderStatus Status => OrderStatus.Paid;

    public override void Ship(Order order, string trackingNumber)
    {
        order.RecordShipment(trackingNumber);
        order.TransitionTo(ShippedState.Instance, $"包裹已发出，运单号 {trackingNumber}。");
    }
}

public sealed class ShippedState : OrderStateBase
{
    public static ShippedState Instance { get; } = new();

    private ShippedState()
    {
    }

    public override OrderStatus Status => OrderStatus.Shipped;

    public override void Complete(Order order)
    {
        order.TransitionTo(CompletedState.Instance, "客户确认收货。");
    }
}

public sealed class CompletedState : OrderStateBase
{
    public static CompletedState Instance { get; } = new();

    private CompletedState()
    {
    }

    public override OrderStatus Status => OrderStatus.Completed;
}

public sealed class CancelledState : OrderStateBase
{
    public static CancelledState Instance { get; } = new();

    private CancelledState()
    {
    }

    public override OrderStatus Status => OrderStatus.Cancelled;
}
