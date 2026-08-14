using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Behavioral;

/// <summary>Caretaker 只能保存这个不透明句柄，不能篡改里面的设备状态。</summary>
public interface ISafetySceneMemento
{
    string Label { get; }
}

/// <summary>Memento Originator：创建并恢复一组设备的安全场景快照。</summary>
public sealed class SafetySceneOriginator
{
    private readonly IReadOnlyList<ISmartDevice> _devices;
    private readonly EventJournal _journal;
    private readonly object _ownerToken = new();
    private int _revision;

    public SafetySceneOriginator(IEnumerable<ISmartDevice> devices, EventJournal journal)
    {
        ArgumentNullException.ThrowIfNull(devices);
        _devices = devices.ToArray();
        if (_devices.Count == 0)
        {
            throw new ArgumentException("安全场景至少需要一个设备。", nameof(devices));
        }

        if (_devices.Select(device => device.Id).Distinct(StringComparer.Ordinal).Count() != _devices.Count)
        {
            throw new ArgumentException("安全场景不能包含重复设备。", nameof(devices));
        }

        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    public ISafetySceneMemento CreateMemento(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var states = _devices.ToDictionary(
            device => device.Id,
            device => device.CurrentState,
            StringComparer.Ordinal);
        var memento = new SafetySceneSnapshot(_ownerToken, ++_revision, label, states);
        _journal.Record("Memento", $"CAPTURE revision={memento.Revision}; label={label}; devices={states.Count}");
        return memento;
    }

    public void Restore(ISafetySceneMemento memento)
    {
        if (memento is not SafetySceneSnapshot snapshot || !ReferenceEquals(snapshot.OwnerToken, _ownerToken))
        {
            throw new ArgumentException("快照不是由当前安全场景创建的。", nameof(memento));
        }

        _journal.Record("Memento", $"RESTORE revision={snapshot.Revision}; label={snapshot.Label}");
        foreach (var device in _devices)
        {
            if (!snapshot.States.TryGetValue(device.Id, out var state))
            {
                throw new InvalidOperationException($"快照缺少设备 {device.Id}。");
            }

            device.RestoreState(state);
        }
    }

    private sealed record SafetySceneSnapshot(
        object OwnerToken,
        int Revision,
        string Label,
        IReadOnlyDictionary<string, DeviceState> States) : ISafetySceneMemento;
}

public sealed class SafetyCheckpointCaretaker(EventJournal journal)
{
    private ISafetySceneMemento? _lastCheckpoint;

    public void Save(ISafetySceneMemento checkpoint)
    {
        _lastCheckpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
        journal.Record("Memento/Caretaker", $"保存检查点：{checkpoint.Label}");
    }

    public ISafetySceneMemento GetLast() =>
        _lastCheckpoint ?? throw new InvalidOperationException("Caretaker 尚未保存检查点。");
}
