using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Behavioral;

public interface IUndoableCommand
{
    string Description { get; }

    void Execute();

    void Undo();
}

public abstract class DeviceCommand : IUndoableCommand
{
    private DeviceState? _before;

    protected DeviceCommand(ISmartDevice device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
    }

    protected ISmartDevice Device { get; }

    public abstract string Description { get; }

    public void Execute()
    {
        if (_before is not null)
        {
            throw new InvalidOperationException("同一个 Command 实例只能执行一次。");
        }

        var snapshot = Device.CurrentState;
        Apply();
        _before = snapshot;
    }

    public void Undo()
    {
        if (_before is null)
        {
            throw new InvalidOperationException("Command 尚未成功执行，无法撤销。");
        }

        Device.RestoreState(_before);
    }

    protected abstract void Apply();
}

public sealed class SetPowerCommand(ISmartDevice device, bool turnOn) : DeviceCommand(device)
{
    public override string Description => $"{Device.DisplayName} 电源 -> {(turnOn ? "开" : "关")}";

    protected override void Apply()
    {
        if (turnOn)
        {
            Device.TurnOn();
        }
        else
        {
            Device.TurnOff();
        }
    }
}

public sealed class SetSettingCommand(ISmartDevice device, int value) : DeviceCommand(device)
{
    public override string Description => $"{Device.DisplayName} {Device.SettingUnit} -> {value}";

    protected override void Apply() => Device.SetSetting(value);
}

/// <summary>Command Invoker：负责执行历史与 Undo，调用方不需要知道接收者的实现。</summary>
public sealed class HomeCommandBus(EventJournal journal)
{
    private readonly Stack<IUndoableCommand> _history = new();

    public int UndoableCommandCount => _history.Count;

    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        journal.Record("Command", $"EXECUTE {command.Description}");
        try
        {
            command.Execute();
            _history.Push(command);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            journal.Record("Command", $"FAILED {command.Description}: {exception.GetType().Name}");
            throw;
        }
    }

    public void UndoLast()
    {
        if (!_history.TryPop(out var command))
        {
            throw new InvalidOperationException("没有可撤销命令。");
        }

        journal.Record("Command", $"UNDO {command.Description}");
        command.Undo();
    }
}
