using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

/// <summary>无法修改的旧设备接口：用数字电源码和华氏温度。</summary>
public sealed class LegacyAirConditioner
{
    private readonly string _serialNumber;
    private readonly EventJournal _journal;

    public LegacyAirConditioner(string serialNumber, EventJournal journal)
    {
        _serialNumber = serialNumber;
        _journal = journal;
    }

    public bool Powered { get; private set; }

    public int TemperatureFahrenheit { get; private set; } = 75;

    public void SwitchPower(int numericCode)
    {
        if (numericCode is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(numericCode), "旧空调只接受 0 或 1。");
        }

        Powered = numericCode == 1;
        _journal.Record("Legacy API", $"{_serialNumber} powerCode={numericCode}");
    }

    public void WriteTemperatureFahrenheit(int temperature)
    {
        if (temperature is < 61 or > 86)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), "旧空调范围为 61..86°F。");
        }

        TemperatureFahrenheit = temperature;
        _journal.Record("Legacy API", $"{_serialNumber} temperature={temperature}°F");
    }
}

/// <summary>Adapter：把旧空调包装成统一的摄氏温度智能设备。</summary>
public sealed class LegacyAirConditionerAdapter : ISmartDevice
{
    private readonly LegacyAirConditioner _legacy;
    private readonly EventJournal _journal;
    private int _temperatureCelsius = 24;

    public LegacyAirConditionerAdapter(
        string id,
        string displayName,
        LegacyAirConditioner legacy,
        EventJournal journal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        DisplayName = displayName;
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string SettingUnit => "目标温度°C";

    public int MinimumSetting => 16;

    public int MaximumSetting => 30;

    public DeviceSensitivity Sensitivity => DeviceSensitivity.Standard;

    public DeviceState CurrentState => new(Id, _legacy.Powered, _temperatureCelsius);

    public void TurnOn()
    {
        _journal.Record("Adapter", $"{DisplayName}：TurnOn -> powerCode=1");
        _legacy.SwitchPower(1);
    }

    public void TurnOff()
    {
        _journal.Record("Adapter", $"{DisplayName}：TurnOff -> powerCode=0");
        _legacy.SwitchPower(0);
    }

    public void SetSetting(int value)
    {
        DeviceGuards.EnsureSettingInRange(this, value);
        _temperatureCelsius = value;
        var fahrenheit = CelsiusToFahrenheit(value);
        _journal.Record("Adapter", $"{DisplayName}：{value}°C -> {fahrenheit}°F");
        _legacy.WriteTemperatureFahrenheit(fahrenheit);
    }

    public void RestoreState(DeviceState state)
    {
        DeviceGuards.EnsureSnapshotMatches(this, state);
        SetSetting(state.Setting);
        if (state.IsOn)
        {
            TurnOn();
        }
        else
        {
            TurnOff();
        }
    }

    private static int CelsiusToFahrenheit(int celsius) =>
        (int)Math.Round((celsius * 9d / 5d) + 32d, MidpointRounding.AwayFromZero);
}
