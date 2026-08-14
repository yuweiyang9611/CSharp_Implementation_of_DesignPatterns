namespace DesignPatterns.Behavioral;

/// <summary>
/// Uses C# events to notify independent services when an order changes state.
/// </summary>
public sealed class ObserverDemo : IPatternDemo
{
    public string Key => "observer";

    public string Name => "Observer / 观察者模式";

    public string Category => "Behavioral";

    public string Intent => "当主题状态变化时，自动通知所有已订阅的观察者。";

    public IReadOnlyList<string> Run()
    {
        var output = new List<string>();
        var order = new Order("ORD-42");
        var email = new EmailNotifier(output);
        var audit = new AuditRecorder(output);

        // Events are the idiomatic .NET form of the Observer pattern.
        order.StatusChanged += email.OnStatusChanged;
        order.StatusChanged += audit.OnStatusChanged;

        order.ChangeStatus(OrderStatus.Paid);
        order.ChangeStatus(OrderStatus.Shipped);

        order.StatusChanged -= email.OnStatusChanged;
        output.Add("Email observer detached.");
        order.ChangeStatus(OrderStatus.Delivered);

        return output;
    }

    private enum OrderStatus
    {
        Created,
        Paid,
        Shipped,
        Delivered
    }

    // Subject: it publishes changes without knowing what each observer does with them.
    private sealed class Order
    {
        internal Order(string number)
        {
            Number = number;
        }

        internal event EventHandler<OrderStatusChangedEventArgs>? StatusChanged;

        internal string Number { get; }

        internal OrderStatus Status { get; private set; } = OrderStatus.Created;

        internal void ChangeStatus(OrderStatus nextStatus)
        {
            var previousStatus = Status;
            Status = nextStatus;
            StatusChanged?.Invoke(
                this,
                new OrderStatusChangedEventArgs(Number, previousStatus, nextStatus));
        }
    }

    private sealed class OrderStatusChangedEventArgs : EventArgs
    {
        internal OrderStatusChangedEventArgs(
            string orderNumber,
            OrderStatus previousStatus,
            OrderStatus currentStatus)
        {
            OrderNumber = orderNumber;
            PreviousStatus = previousStatus;
            CurrentStatus = currentStatus;
        }

        internal string OrderNumber { get; }

        internal OrderStatus PreviousStatus { get; }

        internal OrderStatus CurrentStatus { get; }
    }

    private sealed class EmailNotifier
    {
        private readonly ICollection<string> _output;

        internal EmailNotifier(ICollection<string> output)
        {
            _output = output;
        }

        internal void OnStatusChanged(object? sender, OrderStatusChangedEventArgs args) =>
            _output.Add($"Email: {args.OrderNumber} is now {args.CurrentStatus}.");
    }

    private sealed class AuditRecorder
    {
        private readonly ICollection<string> _output;

        internal AuditRecorder(ICollection<string> output)
        {
            _output = output;
        }

        internal void OnStatusChanged(object? sender, OrderStatusChangedEventArgs args) =>
            _output.Add(
                $"Audit: {args.OrderNumber} changed {args.PreviousStatus} -> {args.CurrentStatus}.");
    }
}
