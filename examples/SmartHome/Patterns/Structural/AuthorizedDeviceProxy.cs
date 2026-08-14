using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

public interface IDeviceAuthorizationPolicy
{
    bool IsAllowed(UserIdentity user, ISmartDevice device, DeviceOperation operation);
}

public sealed class HomeAuthorizationPolicy : IDeviceAuthorizationPolicy
{
    public bool IsAllowed(UserIdentity user, ISmartDevice device, DeviceOperation operation)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(device);

        return user.Role switch
        {
            HomeRole.Owner => true,
            HomeRole.Resident => device.Sensitivity == DeviceSensitivity.Standard || operation == DeviceOperation.PowerOff,
            HomeRole.Guest => device.Sensitivity == DeviceSensitivity.Standard &&
                              operation is DeviceOperation.PowerOn or DeviceOperation.PowerOff,
            _ => false
        };
    }
}

public sealed record AuditEntry(
    int Sequence,
    string User,
    string DeviceId,
    DeviceOperation Operation,
    bool Allowed);

public sealed class DeviceAuditTrail(EventJournal journal)
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries => _entries;

    public void Append(UserIdentity user, ISmartDevice device, DeviceOperation operation, bool allowed)
    {
        var entry = new AuditEntry(_entries.Count + 1, user.Name, device.Id, operation, allowed);
        _entries.Add(entry);
        journal.Record(
            "Proxy/Audit",
            $"user={user.Name}; device={device.Id}; operation={operation}; result={(allowed ? "ALLOW" : "DENY")}");
    }
}

/// <summary>Protection Proxy：调用真实设备前做授权，并将每次写操作写入审计。</summary>
public sealed class AuthorizedDeviceProxy : ISmartDevice
{
    private readonly ISmartDevice _target;
    private readonly UserIdentity _user;
    private readonly IDeviceAuthorizationPolicy _policy;
    private readonly DeviceAuditTrail _audit;

    public AuthorizedDeviceProxy(
        ISmartDevice target,
        UserIdentity user,
        IDeviceAuthorizationPolicy policy,
        DeviceAuditTrail audit)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _user = user ?? throw new ArgumentNullException(nameof(user));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public string Id => _target.Id;

    public string DisplayName => _target.DisplayName;

    public string SettingUnit => _target.SettingUnit;

    public int MinimumSetting => _target.MinimumSetting;

    public int MaximumSetting => _target.MaximumSetting;

    public DeviceSensitivity Sensitivity => _target.Sensitivity;

    public DeviceState CurrentState => _target.CurrentState;

    public void TurnOn() => Invoke(DeviceOperation.PowerOn, _target.TurnOn);

    public void TurnOff() => Invoke(DeviceOperation.PowerOff, _target.TurnOff);

    public void SetSetting(int value) => Invoke(DeviceOperation.Configure, () => _target.SetSetting(value));

    public void RestoreState(DeviceState state) => Invoke(DeviceOperation.Restore, () => _target.RestoreState(state));

    private void Invoke(DeviceOperation operation, Action action)
    {
        var allowed = _policy.IsAllowed(_user, _target, operation);
        _audit.Append(_user, _target, operation, allowed);
        if (!allowed)
        {
            throw new UnauthorizedAccessException(
                $"{_user.Name} ({_user.Role}) 无权对 {DisplayName} 执行 {operation}。");
        }

        action();
    }
}
