using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;
using DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Creational;

public sealed record DeviceCreationContext(
    string Id,
    string DisplayName,
    IDeviceChannel Channel,
    EventJournal Journal);

public sealed record DeviceTypeInfo(string Key, string Description);

/// <summary>
/// Singleton：进程内唯一的设备类型目录。Lazy 提供线程安全的延迟初始化。
/// 注意：这里只让“稳定、无用户态”的工厂目录全局唯一；设备实例本身绝不能做成 Singleton。
/// </summary>
public sealed class DeviceTypeRegistry
{
    private static readonly Lazy<DeviceTypeRegistry> LazyInstance =
        new(() => new DeviceTypeRegistry(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.OrdinalIgnoreCase);

    private DeviceTypeRegistry()
    {
    }

    public static DeviceTypeRegistry Instance => LazyInstance.Value;

    public IReadOnlyList<DeviceTypeInfo> RegisteredTypes
    {
        get
        {
            lock (_gate)
            {
                return _registrations
                    .Select(pair => new DeviceTypeInfo(pair.Key, pair.Value.Description))
                    .OrderBy(info => info.Key, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }

    public bool TryRegister(
        string key,
        string description,
        Func<DeviceCreationContext, ISmartDevice> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            return _registrations.TryAdd(key, new Registration(description, factory));
        }
    }

    public ISmartDevice Create(string key, DeviceCreationContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(context);

        Registration registration;
        lock (_gate)
        {
            if (!_registrations.TryGetValue(key, out registration!))
            {
                throw new KeyNotFoundException($"未注册设备类型：{key}");
            }
        }

        var device = registration.Factory(context);
        context.Journal.Record("Singleton", $"注册表按类型 {key} 创建设备 {device.Id}");
        return device;
    }

    private sealed record Registration(
        string Description,
        Func<DeviceCreationContext, ISmartDevice> Factory);
}
