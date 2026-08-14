using DesignPatterns.TeachingProjects.OnlineStore.Domain;

namespace DesignPatterns.TeachingProjects.OnlineStore.Building;

public interface IOrderBuilder
{
    IOrderBuilder Reset();

    IOrderBuilder ForCustomer(Customer customer);

    IOrderBuilder DeliverTo(ShippingAddress shippingAddress);

    IOrderBuilder AddItemsFrom(ShoppingCart cart);

    IOrderBuilder WithPricing(PricingBreakdown pricing);

    Order Build(string orderNumber);
}
