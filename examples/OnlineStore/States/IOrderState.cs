using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.States;

public interface IOrderState
{
    OrderStatus Status { get; }

    void Pay(Order order, string paymentReference);

    void Ship(Order order, string trackingNumber);

    void Complete(Order order);

    void Cancel(Order order, string reason);
}

public abstract class OrderStateBase : IOrderState
{
    public abstract OrderStatus Status { get; }

    public virtual void Pay(Order order, string paymentReference) => Reject("支付");

    public virtual void Ship(Order order, string trackingNumber) => Reject("发货");

    public virtual void Complete(Order order) => Reject("完成");

    public virtual void Cancel(Order order, string reason) => Reject("取消");

    private void Reject(string operation)
    {
        throw new InvalidOperationException($"订单处于 {Status} 状态，不能执行“{operation}”。");
    }
}
