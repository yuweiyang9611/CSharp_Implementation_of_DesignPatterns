using System.Globalization;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Selects a delivery quotation algorithm at runtime.
/// </summary>
public sealed class StrategyDemo : IPatternDemo
{
    public string Key => "strategy";

    public string Name => "Strategy / 策略模式";

    public string Category => "Behavioral";

    public string Intent => "定义可互换的算法，并让客户端在运行时选择。";

    public IReadOnlyList<string> Run()
    {
        var shipment = new Shipment(WeightKilograms: 4.5m, DistanceKilometers: 18m);
        var planner = new DeliveryPlanner(new StandardDeliveryStrategy());
        var output = new List<string>();

        AddQuote(output, planner, shipment);
        planner.Use(new ExpressDeliveryStrategy());
        AddQuote(output, planner, shipment);
        planner.Use(new ParcelLockerStrategy());
        AddQuote(output, planner, shipment);

        return output;
    }

    private static void AddQuote(
        ICollection<string> output,
        DeliveryPlanner planner,
        Shipment shipment)
    {
        var quote = planner.Calculate(shipment);
        var cost = quote.Cost.ToString("0.00", CultureInfo.InvariantCulture);
        output.Add($"{planner.StrategyName}: cost {cost}, delivery in {quote.BusinessDays} day(s).");
    }

    private sealed record Shipment(decimal WeightKilograms, decimal DistanceKilometers);

    private sealed record DeliveryQuote(decimal Cost, int BusinessDays);

    private interface IDeliveryStrategy
    {
        string Name { get; }

        DeliveryQuote Calculate(Shipment shipment);
    }

    // Context: it delegates the calculation and can swap algorithms without branching.
    private sealed class DeliveryPlanner
    {
        private IDeliveryStrategy _strategy;

        internal DeliveryPlanner(IDeliveryStrategy strategy)
        {
            _strategy = strategy;
        }

        internal string StrategyName => _strategy.Name;

        internal void Use(IDeliveryStrategy strategy) => _strategy = strategy;

        internal DeliveryQuote Calculate(Shipment shipment) => _strategy.Calculate(shipment);
    }

    private sealed class StandardDeliveryStrategy : IDeliveryStrategy
    {
        public string Name => "Standard courier";

        public DeliveryQuote Calculate(Shipment shipment) =>
            new(5m + (shipment.WeightKilograms * 1.2m), 3);
    }

    private sealed class ExpressDeliveryStrategy : IDeliveryStrategy
    {
        public string Name => "Express courier";

        public DeliveryQuote Calculate(Shipment shipment) =>
            new(
                12m +
                (shipment.WeightKilograms * 2m) +
                (shipment.DistanceKilometers * 0.25m),
                1);
    }

    private sealed class ParcelLockerStrategy : IDeliveryStrategy
    {
        public string Name => "Parcel locker";

        public DeliveryQuote Calculate(Shipment shipment) =>
            new(3m + (shipment.WeightKilograms * 0.6m), 2);
    }
}
