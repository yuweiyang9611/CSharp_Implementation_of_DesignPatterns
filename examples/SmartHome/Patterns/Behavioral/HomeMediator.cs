using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

namespace DesignPatterns.TeachingProjects.SmartHome.Patterns.Behavioral;

public enum HomeSignal
{
    EntryOpenedAfterDark,
    WindowOpened,
    SmokeDetected,
    AlertCleared
}

public enum AutomationRole
{
    PathLight,
    Climate,
    Alarm
}

public interface IHomeMediator
{
    void Notify(HomeSensor sender, HomeSignal signal);
}

/// <summary>传感器只认识 Mediator，不认识灯、空调或警报器。</summary>
public sealed class HomeSensor(string name)
{
    private IHomeMediator? _mediator;

    public string Name { get; } = name;

    public void Connect(IHomeMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public void Raise(HomeSignal signal)
    {
        if (_mediator is null)
        {
            throw new InvalidOperationException($"传感器 {Name} 尚未连接家庭中枢。");
        }

        _mediator.Notify(this, signal);
    }
}

/// <summary>Mediator：集中表达跨设备联动，参与者之间保持解耦。</summary>
public sealed class HomeHubMediator(HomeCommandBus commandBus, EventJournal journal) : IHomeMediator
{
    private readonly Dictionary<AutomationRole, ISmartDevice> _devices = [];

    public void Register(AutomationRole role, ISmartDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _devices[role] = device;
        journal.Record("Mediator", $"注册角色 {role} -> {device.DisplayName}");
    }

    public void Notify(HomeSensor sender, HomeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(sender);
        journal.Record("Mediator", $"{sender.Name} 上报 {signal}");

        switch (signal)
        {
            case HomeSignal.EntryOpenedAfterDark:
                PowerAndConfigure(AutomationRole.PathLight, setting: 35);
                break;
            case HomeSignal.WindowOpened:
                commandBus.Execute(new SetPowerCommand(GetRequired(AutomationRole.Climate), turnOn: false));
                break;
            case HomeSignal.SmokeDetected:
                PowerAndConfigure(AutomationRole.PathLight, setting: 100);
                commandBus.Execute(new SetPowerCommand(GetRequired(AutomationRole.Climate), turnOn: false));
                PowerAndConfigure(AutomationRole.Alarm, setting: 3);
                break;
            case HomeSignal.AlertCleared:
                journal.Record("Mediator", "警报解除；状态恢复交给安全场景的 Memento");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(signal), signal, "未知家庭事件。");
        }
    }

    private void PowerAndConfigure(AutomationRole role, int setting)
    {
        var device = GetRequired(role);
        commandBus.Execute(new SetSettingCommand(device, setting));
        commandBus.Execute(new SetPowerCommand(device, turnOn: true));
    }

    private ISmartDevice GetRequired(AutomationRole role) =>
        _devices.TryGetValue(role, out var device)
            ? device
            : throw new InvalidOperationException($"家庭中枢尚未注册角色 {role}。");
}
