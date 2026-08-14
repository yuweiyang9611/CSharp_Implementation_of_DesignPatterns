using DesignPatterns.TeachingProjects.OnlineStore.Application;
using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;

namespace DesignPatterns.TeachingProjects.Tests;

public sealed class OnlineStoreTests
{
    [Fact]
    public void Reserve_WhenAnyProductIsInsufficient_LeavesEveryStockLevelUnchanged()
    {
        var book = new Product("BOOK", "Book", 100m, availableStock: 2);
        var mug = new Product("MUG", "Mug", 50m, availableStock: 1);
        var catalog = new ProductCatalog([book, mug]);
        OrderLine[] request =
        [
            new("BOOK", "Book", 100m, Quantity: 1),
            new("MUG", "Mug", 50m, Quantity: 2),
        ];

        Assert.Throws<InvalidOperationException>(() => catalog.Reserve(request));

        Assert.Equal(2, book.AvailableStock);
        Assert.Equal(1, mug.AvailableStock);
    }

    [Fact]
    public void Reserve_WhenDuplicateSkuTotalExceedsStock_DoesNotPartiallyReserve()
    {
        var book = new Product("BOOK", "Book", 100m, availableStock: 3);
        var catalog = new ProductCatalog([book]);
        OrderLine[] request =
        [
            new("BOOK", "Book", 100m, Quantity: 2),
            new("book", "Book", 100m, Quantity: 2),
        ];

        Assert.Throws<InvalidOperationException>(() => catalog.Reserve(request));

        Assert.Equal(3, book.AvailableStock);
    }

    [Fact]
    public void Checkout_WithNegativeShippingFee_StopsBeforeOrderPaymentAndInventoryChanges()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        var cart = new ShoppingCart();
        cart.Add(book, 1);

        CheckoutResult result = system.Checkout.Checkout(CreateCommand(cart, shippingFee: -1m, balance: 1000m));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Order);
        Assert.Null(result.Payment);
        Assert.Equal(8, book.AvailableStock);
    }

    [Fact]
    public void Checkout_WhenPaymentIsDeclined_CancelsOrderWithoutReservingInventory()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        var cart = new ShoppingCart();
        cart.Add(book, 1);

        CheckoutResult result = system.Checkout.Checkout(CreateCommand(cart, shippingFee: 10m, balance: 0m));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Payment);
        Assert.False(result.Payment.IsApproved);
        Order order = Assert.IsType<Order>(result.Order);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.PaymentReference);
        Assert.Equal(8, book.AvailableStock);
        Assert.Throws<InvalidOperationException>(() => order.Ship("TRACK-1"));
    }

    [Fact]
    public void Checkout_WhenPaymentSucceeds_TransitionsToPaidAndReservesExactQuantity()
    {
        OnlineStoreSystem system = OnlineStoreSystem.Create(echoTrace: false);
        Product book = system.Catalog.GetRequired("BOOK-DP-CS");
        var cart = new ShoppingCart();
        cart.Add(book, 2);

        CheckoutResult result = system.Checkout.Checkout(CreateCommand(cart, shippingFee: 10m, balance: 1000m));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payment);
        Assert.True(result.Payment.IsApproved);
        Order order = Assert.IsType<Order>(result.Order);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(result.Payment.Reference, order.PaymentReference);
        Assert.Equal(6, book.AvailableStock);
    }

    private static CheckoutCommand CreateCommand(ShoppingCart cart, decimal shippingFee, decimal balance) =>
        new(
            cart,
            new Customer("CUSTOMER-1", "Test Customer", IsVip: false),
            new ShippingAddress("Tokyo", "Chiyoda", "1-1", "100-0001"),
            shippingFee,
            new PaymentMethod("WALLET-1", balance));
}
