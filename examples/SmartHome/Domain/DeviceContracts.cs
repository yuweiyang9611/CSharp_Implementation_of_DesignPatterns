namespace DesignPatterns.TeachingProjects.SmartHome.Domain;

/// <summary>设备的可恢复状态。DeviceId 防止把快照误用到另一个设备。</summary>
public sealed record DeviceState(string DeviceId, bool IsOn, int Setting);

public enum DeviceSensitivity
{
    Standard,
    SafetyCritical
}

public enum DeviceOperation
{
    PowerOn,
    PowerOff,
    Configure,
    Restore
}

/// <summary>
/// 教学项目统一的智能设备端口。Setting 的含义由 SettingUnit 描述，
/// 真实系统中也可以进一步拆成 ILight、IThermostat 等更强类型的端口。
/// </summary>
public interface ISmartDevice
{
    string Id { get; }

    string DisplayName { get; }

    string SettingUnit { get; }

    int MinimumSetting { get; }

    int MaximumSetting { get; }

    DeviceSensitivity Sensitivity { get; }

    DeviceState CurrentState { get; }

    void TurnOn();

    void TurnOff();

    void SetSetting(int value);

    void RestoreState(DeviceState state);
}

public static class DeviceGuards
{
    public static void EnsureSettingInRange(ISmartDevice device, int value)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (value < device.MinimumSetting || value > device.MaximumSetting)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"{device.DisplayName} 的设定值必须在 {device.MinimumSetting}..{device.MaximumSetting} {device.SettingUnit} 之间。");
        }
    }

    public static void EnsureSnapshotMatches(ISmartDevice device, DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(state);
        if (!device.Id.Equals(state.DeviceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"快照属于 {state.DeviceId}，不能恢复到 {device.Id}。");
        }
    }
}
