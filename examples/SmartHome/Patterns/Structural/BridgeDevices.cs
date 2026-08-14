using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

/// <summary>Bridge 的实现层：通信通道可独立演进。</summary>
public interface IDeviceChannel
{
    string Name { get; }

    void Send(string address, string command);
}

public sealed class ZigbeeChannel(EventJournal journal) : IDeviceChannel
{
    public string Name => "Zigbee";

    public void Send(string address, string command)
    {
        journal.Record("Bridge/Zigbee", $"{address} <- {command}");
    }
}

public sealed class WifiChannel(EventJournal journal) : IDeviceChannel
{
    public string Name => "Wi-Fi";

    public void Send(string address, string command)
    {
        journal.Record("Bridge/Wi-Fi", $"{address} <- {command}");
    }
}

/// <summary>Bridge 的抽象层：设备语义与 Zigbee/Wi-Fi 细节分离。</summary>
public abstract class ConnectedDevice : ISmartDevice
{
    private readonly IDeviceChannel _channel;
    private bool _isOn;
    private int _setting;

    protected ConnectedDevice(
        string id,
        string displayName,
        IDeviceChannel channel,
        int initialSetting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        Id = id;
        DisplayName = displayName;
        _setting = initialSetting;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public abstract string SettingUnit { get; }

    public abstract int MinimumSetting { get; }

    public abstract int MaximumSetting { get; }

    public abstract DeviceSensitivity Sensitivity { get; }

    public DeviceState CurrentState => new(Id, _isOn, _setting);

    public void TurnOn()
    {
        _isOn = true;
        _channel.Send(Id, $"POWER ON ({DisplayName})");
    }

    public void TurnOff()
    {
        _isOn = false;
        _channel.Send(Id, $"POWER OFF ({DisplayName})");
    }

    public void SetSetting(int value)
    {
        DeviceGuards.EnsureSettingInRange(this, value);
        _setting = value;
        _channel.Send(Id, $"SET {SettingUnit}={value}");
    }

    public void RestoreState(DeviceState state)
    {
        DeviceGuards.EnsureSnapshotMatches(this, state);
        DeviceGuards.EnsureSettingInRange(this, state.Setting);
        _isOn = state.IsOn;
        _setting = state.Setting;
        _channel.Send(Id, $"SYNC power={(_isOn ? "ON" : "OFF")};{SettingUnit}={_setting}");
    }
}

public sealed class DimmableLight(
    string id,
    string displayName,
    IDeviceChannel channel)
    : ConnectedDevice(id, displayName, channel, initialSetting: 20)
{
    public override string SettingUnit => "亮度%";

    public override int MinimumSetting => 1;

    public override int MaximumSetting => 100;

    public override DeviceSensitivity Sensitivity => DeviceSensitivity.Standard;
}

public sealed class SmartSiren(
    string id,
    string displayName,
    IDeviceChannel channel)
    : ConnectedDevice(id, displayName, channel, initialSetting: 1)
{
    public override string SettingUnit => "警报等级";

    public override int MinimumSetting => 1;

    public override int MaximumSetting => 3;

    public override DeviceSensitivity Sensitivity => DeviceSensitivity.SafetyCritical;
}
