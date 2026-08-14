using DesignPatterns.TeachingProjects.SmartHome.Domain;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

public enum HomeGroupKind
{
    Home,
    Floor,
    Room
}

/// <summary>Composite 的共同接口：叶子设备与组合节点可以被一致操作。</summary>
public interface IHomeComponent
{
    string Name { get; }

    int DeviceCount { get; }

    IEnumerable<ISmartDevice> EnumerateDevices();

    void ApplyToDevices(Action<ISmartDevice> operation);

    void WriteTree(TextWriter writer, int depth = 0);
}

public sealed class DeviceNode(ISmartDevice device) : IHomeComponent
{
    private readonly ISmartDevice _device = device ?? throw new ArgumentNullException(nameof(device));

    public string Name => _device.DisplayName;

    public int DeviceCount => 1;

    public IEnumerable<ISmartDevice> EnumerateDevices()
    {
        yield return _device;
    }

    public void ApplyToDevices(Action<ISmartDevice> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        operation(_device);
    }

    public void WriteTree(TextWriter writer, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine($"{new string(' ', depth * 2)}- 设备：{_device.DisplayName} [{_device.Id}]");
    }
}

public sealed class HomeGroup : IHomeComponent
{
    private readonly List<IHomeComponent> _children = [];

    public HomeGroup(string name, HomeGroupKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public HomeGroupKind Kind { get; }

    public int DeviceCount => _children.Sum(child => child.DeviceCount);

    public HomeGroup Add(IHomeComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (ReferenceEquals(component, this))
        {
            throw new InvalidOperationException("组合节点不能包含自身。");
        }

        _children.Add(component);
        return this;
    }

    public IEnumerable<ISmartDevice> EnumerateDevices() =>
        _children.SelectMany(child => child.EnumerateDevices());

    public void ApplyToDevices(Action<ISmartDevice> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        foreach (var child in _children)
        {
            child.ApplyToDevices(operation);
        }
    }

    public void WriteTree(TextWriter writer, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine($"{new string(' ', depth * 2)}+ {Kind}：{Name}（{DeviceCount} 台设备）");
        foreach (var child in _children)
        {
            child.WriteTree(writer, depth + 1);
        }
    }
}
