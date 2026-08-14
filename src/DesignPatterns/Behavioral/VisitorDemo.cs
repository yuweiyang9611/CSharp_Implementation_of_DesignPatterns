using System.Globalization;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Adds pricing and shipping operations to cart item types without modifying those types.
/// </summary>
public sealed class VisitorDemo : IPatternDemo
{
    public string Key => "visitor";

    public string Name => "Visitor / 访问者模式";

    public string Category => "Behavioral";

    public string Intent => "在不修改元素类型的前提下，为对象结构增加新操作。";

    public IReadOnlyList<string> Run()
    {
        IReadOnlyList<ICartItem> cart =
        [
            new Book("Design Patterns", 50m, 1.2m),
            new ElectronicDevice("Mechanical Keyboard", 80m, 0.8m)
        ];

        var output = new List<string>();
        var pricing = new PricingVisitor(output);
        foreach (var item in cart)
        {
            item.Accept(pricing);
        }

        output.Add($"Pricing total: {FormatMoney(pricing.Total)}.");

        var shipping = new ShippingVisitor(output);
        foreach (var item in cart)
        {
            item.Accept(shipping);
        }

        output.Add($"Shipping total: {FormatMoney(shipping.Total)}.");
        return output;
    }

    private static string FormatMoney(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private interface ICartItem
    {
        void Accept(ICartVisitor visitor);
    }

    // Visitor overloads provide double dispatch: behavior depends on visitor and element type.
    private interface ICartVisitor
    {
        void Visit(Book book);

        void Visit(ElectronicDevice device);
    }

    private sealed record Book(string Title, decimal Price, decimal WeightKilograms) : ICartItem
    {
        public void Accept(ICartVisitor visitor) => visitor.Visit(this);
    }

    private sealed record ElectronicDevice(
        string Name,
        decimal Price,
        decimal WeightKilograms) : ICartItem
    {
        public void Accept(ICartVisitor visitor) => visitor.Visit(this);
    }

    private sealed class PricingVisitor : ICartVisitor
    {
        private readonly ICollection<string> _output;

        internal PricingVisitor(ICollection<string> output)
        {
            _output = output;
        }

        internal decimal Total { get; private set; }

        public void Visit(Book book)
        {
            var discountedPrice = book.Price * 0.9m;
            Total += discountedPrice;
            _output.Add($"Pricing book '{book.Title}': {FormatMoney(discountedPrice)} after discount.");
        }

        public void Visit(ElectronicDevice device)
        {
            var priceWithRecyclingFee = device.Price + 2m;
            Total += priceWithRecyclingFee;
            _output.Add(
                $"Pricing device '{device.Name}': {FormatMoney(priceWithRecyclingFee)} including recycling fee.");
        }
    }

    private sealed class ShippingVisitor : ICartVisitor
    {
        private readonly ICollection<string> _output;

        internal ShippingVisitor(ICollection<string> output)
        {
            _output = output;
        }

        internal decimal Total { get; private set; }

        public void Visit(Book book)
        {
            var cost = 2m + book.WeightKilograms;
            Total += cost;
            _output.Add($"Shipping book '{book.Title}': {FormatMoney(cost)} via media mail.");
        }

        public void Visit(ElectronicDevice device)
        {
            var cost = 5m + (device.WeightKilograms * 2m);
            Total += cost;
            _output.Add($"Shipping device '{device.Name}': {FormatMoney(cost)} with insurance.");
        }
    }
}
