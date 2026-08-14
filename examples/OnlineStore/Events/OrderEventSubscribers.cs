using DesignPatterns.TeachingProjects.OnlineStore.Application;
using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Events;

public sealed class InventoryReservationSubscriber(
    ProductCatalog catalog,
    ICheckoutTrace trace) : IOrderEventSubscriber
{
    public string Name => "库存预留服务";

    public void OnEvent(IOrderEvent orderEvent)
    {
        if (orderEvent is not OrderStatusChangedEvent { Current: OrderStatus.Paid } paid)
        {
            return;
        }

        catalog.Reserve(paid.Order.Lines);
        trace.Add($"[Observer:{Name}] 订单 {paid.Order.Number} 已支付，扣减并预留库存。");
    }
}

public sealed class CustomerNotificationSubscriber(ICheckoutTrace trace) : IOrderEventSubscriber
{
    private readonly List<string> _messages = [];

    public string Name => "客户通知服务";

    public IReadOnlyList<string> Messages => _messages;

    public void OnEvent(IOrderEvent orderEvent)
    {
        string? message = orderEvent switch
        {
            OrderPlacedEvent placed => $"{placed.Order.Customer.Name}，订单 {placed.Order.Number} 已创建。",
            OrderStatusChangedEvent changed =>
                $"{changed.Order.Customer.Name}，订单状态已更新为 {changed.Current}。",
            _ => null,
        };

        if (message is not null)
        {
            _messages.Add(message);
            trace.Add($"[Observer:{Name}] {message}");
        }
    }
}

public sealed class AuditLogSubscriber(ICheckoutTrace trace) : IOrderEventSubscriber
{
    public string Name => "审计日志服务";

    public void OnEvent(IOrderEvent orderEvent)
    {
        trace.Add($"[Observer:{Name}] 已记录：{orderEvent.Describe()}");
    }
}
