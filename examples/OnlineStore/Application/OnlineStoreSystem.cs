using DesignPatterns.TeachingProjects.OnlineStore.Building;
using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Events;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;
using DesignPatterns.TeachingProjects.OnlineStore.Pricing;
using DesignPatterns.TeachingProjects.OnlineStore.Validation;

namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public sealed class OnlineStoreSystem
{
    private OnlineStoreSystem(
        CheckoutFacade checkout,
        ProductCatalog catalog,
        CustomerNotificationSubscriber notifications,
        CheckoutTrace trace)
    {
        Checkout = checkout;
        Catalog = catalog;
        Notifications = notifications;
        Trace = trace;
    }

    public CheckoutFacade Checkout { get; }

    public ProductCatalog Catalog { get; }

    public CustomerNotificationSubscriber Notifications { get; }

    public CheckoutTrace Trace { get; }

    public static OnlineStoreSystem Create(bool echoTrace)
    {
        CheckoutTrace trace = new(echoTrace);
        ProductCatalog catalog = ProductCatalog.CreateDemoCatalog();
        OrderEventPublisher publisher = new(trace);
        CustomerNotificationSubscriber notifications = new(trace);

        publisher.Subscribe(new InventoryReservationSubscriber(catalog, trace));
        publisher.Subscribe(notifications);
        publisher.Subscribe(new AuditLogSubscriber(trace));

        CheckoutValidationChain validation = CheckoutValidationChain.CreateDefault(catalog, trace);
        PricingStrategySelector pricingSelector = new(
            new StandardPricingStrategy(),
            new VipPricingStrategy(discountRate: 0.10m));
        OrderBuilder builder = new(publisher, trace);
        PaymentProcessorCreator paymentCreator = new WalletPaymentProcessorCreator(trace);

        CheckoutFacade facade = new(
            validation,
            pricingSelector,
            builder,
            paymentCreator,
            publisher,
            new SequentialOrderNumberGenerator(),
            trace);

        return new OnlineStoreSystem(facade, catalog, notifications, trace);
    }
}
