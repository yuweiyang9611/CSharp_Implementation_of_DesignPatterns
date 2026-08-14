using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Events;

public interface IOrderEvent
{
    string Describe();
}

public sealed record OrderPlacedEvent(Order Order) : IOrderEvent
{
    public string Describe() => $"订单 {Order.Number} 已创建，等待支付。";
}

public sealed record OrderStatusChangedEvent(
    Order Order,
    OrderStatus Previous,
    OrderStatus Current,
    string Reason) : IOrderEvent
{
    public string Describe() =>
        $"订单 {Order.Number} 从 {Previous} 变为 {Current}：{Reason}";
}
