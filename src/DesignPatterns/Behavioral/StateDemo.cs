namespace DesignPatterns.Behavioral;

/// <summary>
/// Lets an order change behavior as it moves through its lifecycle.
/// </summary>
public sealed class StateDemo : IPatternDemo
{
    public string Key => "state";

    public string Name => "State / 状态模式";

    public string Category => "Behavioral";

    public string Intent => "让对象在内部状态改变时切换其行为。";

    public IReadOnlyList<string> Run()
    {
        var output = new List<string>();
        var firstOrder = new PurchaseOrder("ORD-100", output);

        output.Add($"{firstOrder.Number} starts in {firstOrder.StateName}.");
        firstOrder.Pay();
        firstOrder.Pay();
        firstOrder.Ship();
        firstOrder.Cancel();

        var secondOrder = new PurchaseOrder("ORD-200", output);
        output.Add($"{secondOrder.Number} starts in {secondOrder.StateName}.");
        secondOrder.Cancel();
        secondOrder.Pay();

        return output;
    }

    private interface IOrderState
    {
        string Name { get; }

        void Pay(PurchaseOrder order);

        void Ship(PurchaseOrder order);

        void Cancel(PurchaseOrder order);
    }

    // Context: every operation is delegated to the current state object.
    private sealed class PurchaseOrder
    {
        private readonly ICollection<string> _output;
        private IOrderState _state = AwaitingPaymentState.Instance;

        internal PurchaseOrder(string number, ICollection<string> output)
        {
            Number = number;
            _output = output;
        }

        internal string Number { get; }

        internal string StateName => _state.Name;

        internal void Pay() => _state.Pay(this);

        internal void Ship() => _state.Ship(this);

        internal void Cancel() => _state.Cancel(this);

        internal void TransitionTo(IOrderState state)
        {
            _state = state;
        }

        internal void Log(string message) => _output.Add($"{Number}: {message}");
    }

    private sealed class AwaitingPaymentState : IOrderState
    {
        internal static readonly AwaitingPaymentState Instance = new();

        private AwaitingPaymentState()
        {
        }

        public string Name => "AwaitingPayment";

        public void Pay(PurchaseOrder order)
        {
            order.Log("payment captured; state -> Paid.");
            order.TransitionTo(PaidState.Instance);
        }

        public void Ship(PurchaseOrder order) => order.Log("cannot ship before payment.");

        public void Cancel(PurchaseOrder order)
        {
            order.Log("cancelled before payment; state -> Cancelled.");
            order.TransitionTo(CancelledState.Instance);
        }
    }

    private sealed class PaidState : IOrderState
    {
        internal static readonly PaidState Instance = new();

        private PaidState()
        {
        }

        public string Name => "Paid";

        public void Pay(PurchaseOrder order) => order.Log("payment ignored; already paid.");

        public void Ship(PurchaseOrder order)
        {
            order.Log("shipment created; state -> Shipped.");
            order.TransitionTo(ShippedState.Instance);
        }

        public void Cancel(PurchaseOrder order)
        {
            order.Log("payment refunded; state -> Cancelled.");
            order.TransitionTo(CancelledState.Instance);
        }
    }

    private sealed class ShippedState : IOrderState
    {
        internal static readonly ShippedState Instance = new();

        private ShippedState()
        {
        }

        public string Name => "Shipped";

        public void Pay(PurchaseOrder order) => order.Log("payment ignored; already shipped.");

        public void Ship(PurchaseOrder order) => order.Log("shipment ignored; already shipped.");

        public void Cancel(PurchaseOrder order) => order.Log("cannot cancel after shipment.");
    }

    private sealed class CancelledState : IOrderState
    {
        internal static readonly CancelledState Instance = new();

        private CancelledState()
        {
        }

        public string Name => "Cancelled";

        public void Pay(PurchaseOrder order) => order.Log("cannot pay a cancelled order.");

        public void Ship(PurchaseOrder order) => order.Log("cannot ship a cancelled order.");

        public void Cancel(PurchaseOrder order) => order.Log("cancellation ignored; already cancelled.");
    }
}
