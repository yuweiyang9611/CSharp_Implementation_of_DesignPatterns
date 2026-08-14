using DesignPatterns.TeachingProjects.OnlineStore.Application;
using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Events;

namespace DesignPatterns.TeachingProjects.OnlineStore.Building;

public sealed class OrderBuilder(
    IOrderEventPublisher eventPublisher,
    ICheckoutTrace trace) : IOrderBuilder
{
    private Customer? _customer;
    private ShippingAddress? _shippingAddress;
    private PricingBreakdown? _pricing;
    private readonly List<OrderLine> _lines = [];

    public IOrderBuilder Reset()
    {
        _customer = null;
        _shippingAddress = null;
        _pricing = null;
        _lines.Clear();
        return this;
    }

    public IOrderBuilder ForCustomer(Customer customer)
    {
        _customer = customer;
        return this;
    }

    public IOrderBuilder DeliverTo(ShippingAddress shippingAddress)
    {
        _shippingAddress = shippingAddress;
        return this;
    }

    public IOrderBuilder AddItemsFrom(ShoppingCart cart)
    {
        _lines.AddRange(
            cart.Items.Select(item => new OrderLine(
                item.Product.Sku,
                item.Product.Name,
                item.Product.UnitPrice,
                item.Quantity)));
        return this;
    }

    public IOrderBuilder WithPricing(PricingBreakdown pricing)
    {
        _pricing = pricing;
        return this;
    }

    public Order Build(string orderNumber)
    {
        Customer customer = _customer ?? throw new InvalidOperationException("尚未设置订单客户。");
        ShippingAddress address = _shippingAddress ?? throw new InvalidOperationException("尚未设置收货地址。");
        PricingBreakdown pricing = _pricing ?? throw new InvalidOperationException("尚未设置价格明细。");
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("订单至少需要一条商品明细。");
        }

        Order order = new(
            orderNumber,
            customer,
            address,
            _lines.ToArray(),
            pricing,
            eventPublisher);
        trace.Add($"[Builder] 分步组装订单 {order.Number}：客户、地址、{order.Lines.Count} 条商品与价格快照均已就绪。");
        Reset();
        return order;
    }
}
