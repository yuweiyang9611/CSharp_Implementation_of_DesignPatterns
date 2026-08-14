using System.Globalization;

namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates a checkout facade that coordinates inventory, payment, and shipping subsystems.
/// </summary>
public sealed class FacadeDemo : IPatternDemo
{
    public string Key => "facade";

    public string Name => "Facade / 外观模式";

    public string Category => "Structural";

    public string Intent => "为复杂子系统提供一个更简单、面向用例的统一入口。";

    public IReadOnlyList<string> Run()
    {
        var checkout = new CheckoutFacade(
            new InventoryService(),
            new PaymentGateway(),
            new ShippingService());

        var order = new Order(
            OrderId: "SO-2048",
            Sku: "BOOK-DP-CS",
            Quantity: 1,
            CustomerId: "C-007",
            Amount: 128m,
            Destination: "Tokyo");

        return checkout.PlaceOrder(order);
    }

    private sealed record Order(
        string OrderId,
        string Sku,
        int Quantity,
        string CustomerId,
        decimal Amount,
        string Destination);

    // Subsystems remain independently useful, but clients need not understand their orchestration order.
    private sealed class InventoryService
    {
        public string Reserve(string sku, int quantity) => $"库存: 已预留 {sku} x {quantity}";
    }

    private sealed class PaymentGateway
    {
        public string Charge(string customerId, decimal amount) =>
            $"支付: 客户 {customerId} 扣款 {Money(amount)}, 授权号 PAY-8842";
    }

    private sealed class ShippingService
    {
        public string CreateShipment(string destination) =>
            $"物流: 已创建发往 {destination} 的运单 TRACK-3105";
    }

    // Facade presents one transaction-sized operation and owns the collaboration between subsystems.
    private sealed class CheckoutFacade(
        InventoryService inventory,
        PaymentGateway payment,
        ShippingService shipping)
    {
        public IReadOnlyList<string> PlaceOrder(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            return
            [
                $"开始结算订单 {order.OrderId}",
                inventory.Reserve(order.Sku, order.Quantity),
                payment.Charge(order.CustomerId, order.Amount),
                shipping.CreateShipment(order.Destination),
                $"订单 {order.OrderId} 结算完成"
            ];
        }
    }

    private static string Money(decimal amount) =>
        $"CNY {amount.ToString("0.00", CultureInfo.InvariantCulture)}";
}
