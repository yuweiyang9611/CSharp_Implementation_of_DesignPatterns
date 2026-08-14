using System.Globalization;

namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates adapting a legacy Fahrenheit device to the Celsius contract used by new code.
/// </summary>
public sealed class AdapterDemo : IPatternDemo
{
    public string Key => "adapter";

    public string Name => "Adapter / 适配器模式";

    public string Category => "Structural";

    public string Intent => "把已有类型的接口转换为客户端期望的接口，让原本不兼容的类型协同工作。";

    public IReadOnlyList<string> Run()
    {
        var legacyGateway = new LegacyFahrenheitGateway();
        ITemperatureSensor sensor = new FahrenheitSensorAdapter(
            legacyGateway,
            deviceId: 7,
            location: "冷藏库 A");

        var rawFahrenheit = legacyGateway.ReadFahrenheit(7);
        var reading = sensor.Read();
        var status = reading.Celsius is >= 2 and <= 8 ? "温度正常" : "需要告警";

        return
        [
            $"旧设备原始读数: {rawFahrenheit.ToString("0.0", CultureInfo.InvariantCulture)} °F",
            $"新接口位置: {reading.Location}",
            $"适配后的读数: {reading.Celsius.ToString("0.0", CultureInfo.InvariantCulture)} °C",
            $"冷链检查结果: {status}"
        ];
    }

    private sealed record TemperatureReading(string Location, double Celsius);

    // Target: the contract expected by the modern monitoring application.
    private interface ITemperatureSensor
    {
        TemperatureReading Read();
    }

    // Adaptee: its API and unit cannot be changed because an old device driver depends on them.
    private sealed class LegacyFahrenheitGateway
    {
        public double ReadFahrenheit(int deviceId) => deviceId switch
        {
            7 => 41.0,
            8 => 50.0,
            _ => throw new ArgumentOutOfRangeException(nameof(deviceId), deviceId, "Unknown device.")
        };
    }

    // Adapter translates both the method shape and the temperature unit.
    private sealed class FahrenheitSensorAdapter(
        LegacyFahrenheitGateway gateway,
        int deviceId,
        string location) : ITemperatureSensor
    {
        public TemperatureReading Read()
        {
            var fahrenheit = gateway.ReadFahrenheit(deviceId);
            var celsius = (fahrenheit - 32d) * 5d / 9d;
            return new TemperatureReading(location, celsius);
        }
    }
}
