using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;

namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public sealed record CheckoutCommand(
    ShoppingCart Cart,
    Customer Customer,
    ShippingAddress ShippingAddress,
    decimal ShippingFee,
    PaymentMethod PaymentMethod);
