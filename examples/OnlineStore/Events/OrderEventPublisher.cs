using DesignPatterns.TeachingProjects.OnlineStore.Application;

namespace DesignPatterns.TeachingProjects.OnlineStore.Events;

public interface IOrderEventSubscriber
{
    string Name { get; }

    void OnEvent(IOrderEvent orderEvent);
}

public interface IOrderEventPublisher
{
    void Subscribe(IOrderEventSubscriber subscriber);

    void Unsubscribe(IOrderEventSubscriber subscriber);

    void Publish(IOrderEvent orderEvent);
}

public sealed class OrderEventPublisher(ICheckoutTrace trace) : IOrderEventPublisher
{
    private readonly List<IOrderEventSubscriber> _subscribers = [];

    public void Subscribe(IOrderEventSubscriber subscriber)
    {
        if (!_subscribers.Contains(subscriber))
        {
            _subscribers.Add(subscriber);
        }
    }

    public void Unsubscribe(IOrderEventSubscriber subscriber)
    {
        _subscribers.Remove(subscriber);
    }

    public void Publish(IOrderEvent orderEvent)
    {
        trace.Add($"[Observer] 发布事件：{orderEvent.Describe()}");
        foreach (IOrderEventSubscriber subscriber in _subscribers.ToArray())
        {
            subscriber.OnEvent(orderEvent);
        }
    }
}
