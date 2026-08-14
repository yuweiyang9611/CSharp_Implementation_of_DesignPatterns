using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;
using DesignPatterns.TeachingProjects.SmartHome.Patterns.Behavioral;
using DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

namespace DesignPatterns.TeachingProjects.Tests;

public sealed class SmartHomeTests
{
    [Fact]
    public void Guest_WhenTurningOnSafetyCriticalDevice_IsDeniedWithoutChangingDevice()
    {
        EventJournal journal = CreateJournal();
        var siren = new SmartSiren("siren", "Siren", new WifiChannel(journal));
        var audit = new DeviceAuditTrail(journal);
        var proxy = new AuthorizedDeviceProxy(
            siren,
            new UserIdentity("Guest", HomeRole.Guest),
            new HomeAuthorizationPolicy(),
            audit);

        Assert.Throws<UnauthorizedAccessException>(proxy.TurnOn);

        Assert.False(siren.CurrentState.IsOn);
        AuditEntry denied = Assert.Single(audit.Entries);
        Assert.Equal(DeviceOperation.PowerOn, denied.Operation);
        Assert.False(denied.Allowed);
    }

    [Fact]
    public void Guest_CanPowerStandardDeviceButCannotReconfigureIt()
    {
        EventJournal journal = CreateJournal();
        var light = new DimmableLight("light", "Hall Light", new ZigbeeChannel(journal));
        var audit = new DeviceAuditTrail(journal);
        var proxy = new AuthorizedDeviceProxy(
            light,
            new UserIdentity("Guest", HomeRole.Guest),
            new HomeAuthorizationPolicy(),
            audit);

        proxy.TurnOn();
        Assert.Throws<UnauthorizedAccessException>(() => proxy.SetSetting(80));

        Assert.Equal(new DeviceState("light", IsOn: true, Setting: 20), light.CurrentState);
        Assert.Collection(
            audit.Entries,
            allowed => Assert.True(allowed.Allowed),
            denied => Assert.False(denied.Allowed));
    }

    [Fact]
    public void Command_WhenExecutionFails_DoesNotBecomeUndoableOrChangeReceiver()
    {
        EventJournal journal = CreateJournal();
        var light = new DimmableLight("light", "Hall Light", new ZigbeeChannel(journal));
        var bus = new HomeCommandBus(journal);
        DeviceState before = light.CurrentState;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bus.Execute(new SetSettingCommand(light, value: 101)));

        Assert.Equal(before, light.CurrentState);
        Assert.Equal(0, bus.UndoableCommandCount);
        Assert.Throws<InvalidOperationException>(bus.UndoLast);
    }

    [Fact]
    public void UndoLast_RestoresTheStateCapturedBeforeCommandExecution()
    {
        EventJournal journal = CreateJournal();
        var light = new DimmableLight("light", "Hall Light", new ZigbeeChannel(journal));
        var bus = new HomeCommandBus(journal);
        DeviceState before = light.CurrentState;

        bus.Execute(new SetSettingCommand(light, value: 75));
        Assert.Equal(75, light.CurrentState.Setting);

        bus.UndoLast();

        Assert.Equal(before, light.CurrentState);
        Assert.Equal(0, bus.UndoableCommandCount);
    }

    [Fact]
    public void Memento_RestoresEveryDeviceInTheSafetyScene()
    {
        EventJournal journal = CreateJournal();
        var light = new DimmableLight("light", "Hall Light", new ZigbeeChannel(journal));
        var siren = new SmartSiren("siren", "Siren", new WifiChannel(journal));
        light.TurnOn();
        light.SetSetting(40);
        siren.TurnOff();
        DeviceState lightBefore = light.CurrentState;
        DeviceState sirenBefore = siren.CurrentState;
        var scene = new SafetySceneOriginator([light, siren], journal);
        ISafetySceneMemento checkpoint = scene.CreateMemento("before-alert");

        light.SetSetting(100);
        light.TurnOff();
        siren.SetSetting(3);
        siren.TurnOn();
        scene.Restore(checkpoint);

        Assert.Equal(lightBefore, light.CurrentState);
        Assert.Equal(sirenBefore, siren.CurrentState);
    }

    [Fact]
    public void Memento_FromAnotherOriginator_IsRejectedWithoutChangingState()
    {
        EventJournal journal = CreateJournal();
        var light = new DimmableLight("light", "Hall Light", new ZigbeeChannel(journal));
        var firstScene = new SafetySceneOriginator([light], journal);
        var secondScene = new SafetySceneOriginator([light], journal);
        ISafetySceneMemento foreignCheckpoint = secondScene.CreateMemento("foreign");
        light.SetSetting(65);
        DeviceState beforeRestoreAttempt = light.CurrentState;

        Assert.Throws<ArgumentException>(() => firstScene.Restore(foreignCheckpoint));

        Assert.Equal(beforeRestoreAttempt, light.CurrentState);
    }

    private static EventJournal CreateJournal() => new(TextWriter.Null);
}
