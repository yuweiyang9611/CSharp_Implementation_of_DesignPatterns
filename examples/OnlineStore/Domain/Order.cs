using DesignPatterns.TeachingProjects.OnlineStore.Events;
using DesignPatterns.TeachingProjects.OnlineStore.States;

namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed class Order
{
    private readonly IOrderEventPublisher _eventPublisher;
    private IOrderState _state = AwaitingPaymentState.Instance;

    public Order(
        string number,
        Customer customer,
        ShippingAddress shippingAddress,
        IReadOnlyList<OrderLine> lines,
        PricingBreakdown pricing,
        IOrderEventPublisher eventPublisher)
    {
        Number = number;
        Customer = customer;
        ShippingAddress = shippingAddress;
        Lines = lines;
        Pricing = pricing;
        _eventPublisher = eventPublisher;
    }

    public string Number { get; }

    public Customer Customer { get; }

    public ShippingAddress ShippingAddress { get; }

    public IReadOnlyList<OrderLine> Lines { get; }

    public PricingBreakdown Pricing { get; }

    public OrderStatus Status => _state.Status;

    public string? PaymentReference { get; private set; }

    public string? TrackingNumber { get; private set; }

    public void Pay(string paymentReference) => _state.Pay(this, paymentReference);

    public void Ship(string trackingNumber) => _state.Ship(this, trackingNumber);

    public void Complete() => _state.Complete(this);

    public void Cancel(string reason) => _state.Cancel(this, reason);

    internal void RecordPayment(string paymentReference)
    {
        PaymentReference = paymentReference;
    }

    internal void RecordShipment(string trackingNumber)
    {
        TrackingNumber = trackingNumber;
    }

    internal void TransitionTo(IOrderState nextState, string reason)
    {
        OrderStatus previous = _state.Status;
        _state = nextState;
        _eventPublisher.Publish(new OrderStatusChangedEvent(this, previous, nextState.Status, reason));
    }
}
