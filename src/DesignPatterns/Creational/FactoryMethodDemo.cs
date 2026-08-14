using System.Globalization;

namespace DesignPatterns.Creational;

/// <summary>
/// Demonstrates how subclasses select a carrier while the base workflow keeps the algorithm stable.
/// </summary>
public sealed class FactoryMethodDemo : IPatternDemo
{
    public string Key => "factory-method";

    public string Name => "Factory Method / 工厂方法模式";

    public string Category => "Creational";

    public string Intent => "定义创建产品的接口，把具体产品的选择延迟到子类。";

    public IReadOnlyList<string> Run()
    {
        var shipment = new Shipment("SO-2026-0714", "Tokyo", 2.5m);
        ShipmentWorkflow normalWorkflow = new RoadShipmentWorkflow();
        ShipmentWorkflow urgentWorkflow = new AirShipmentWorkflow();

        return
        [
            "普通订单使用公路工厂:",
            .. normalWorkflow.Plan(shipment),
            "加急订单使用航空工厂:",
            .. urgentWorkflow.Plan(shipment)
        ];
    }

    private sealed record Shipment(string OrderId, string Destination, decimal WeightKg);

    // Product: every carrier can create a label and provide an estimate.
    private interface ICarrier
    {
        string Name { get; }

        string CreateLabel(Shipment shipment);

        int EstimateDeliveryDays(Shipment shipment);
    }

    private sealed class TruckCarrier : ICarrier
    {
        public string Name => "Green Truck";

        public string CreateLabel(Shipment shipment) =>
            $"标签: ROAD-{shipment.OrderId} -> {shipment.Destination}, " +
            $"{shipment.WeightKg.ToString("0.0", CultureInfo.InvariantCulture)}kg";

        public int EstimateDeliveryDays(Shipment shipment) => shipment.WeightKg > 10m ? 4 : 3;
    }

    private sealed class ExpressAirCarrier : ICarrier
    {
        public string Name => "Swift Air";

        public string CreateLabel(Shipment shipment) =>
            $"标签: AIR-{shipment.OrderId} -> {shipment.Destination}, " +
            $"{shipment.WeightKg.ToString("0.0", CultureInfo.InvariantCulture)}kg";

        public int EstimateDeliveryDays(Shipment shipment) => shipment.Destination == "Tokyo" ? 1 : 2;
    }

    // Creator: Plan is the stable template; CreateCarrier is the factory method.
    private abstract class ShipmentWorkflow
    {
        public IReadOnlyList<string> Plan(Shipment shipment)
        {
            ArgumentNullException.ThrowIfNull(shipment);
            var carrier = CreateCarrier();

            return
            [
                $"承运商: {carrier.Name}",
                carrier.CreateLabel(shipment),
                $"预计送达: {carrier.EstimateDeliveryDays(shipment)} 天"
            ];
        }

        protected abstract ICarrier CreateCarrier();
    }

    private sealed class RoadShipmentWorkflow : ShipmentWorkflow
    {
        protected override ICarrier CreateCarrier() => new TruckCarrier();
    }

    private sealed class AirShipmentWorkflow : ShipmentWorkflow
    {
        protected override ICarrier CreateCarrier() => new ExpressAirCarrier();
    }
}
