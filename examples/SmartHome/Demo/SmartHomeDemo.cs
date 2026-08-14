using DesignPatterns.TeachingProjects.SmartHome.Domain;
using DesignPatterns.TeachingProjects.SmartHome.Infrastructure;
using DesignPatterns.TeachingProjects.SmartHome.Patterns.Behavioral;
using DesignPatterns.TeachingProjects.SmartHome.Patterns.Creational;
using DesignPatterns.TeachingProjects.SmartHome.Patterns.Structural;

namespace DesignPatterns.TeachingProjects.SmartHome.Demo;

public sealed record SmartHomeDemoResult(
    bool SingletonIsStable,
    int RegisteredDeviceTypeCount,
    bool AdapterConvertedCelsius,
    bool BridgeUsedTwoChannels,
    bool CompositeRoundTripRestored,
    int CompositeDeviceCount,
    bool ProxyDeniedGuest,
    bool CommandUndoRestored,
    bool MediatorEntryRuleApplied,
    bool EmergencyRuleApplied,
    bool MementoRestored,
    int AuditEntryCount,
    int DeniedAuditEntryCount);

public static class SmartHomeDemo
{
    public static SmartHomeDemoResult Run(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("============================================================");
        writer.WriteLine(" 智能家居自动化：8 种设计模式协作教学场景");
        writer.WriteLine("============================================================");

        var journal = new EventJournal(writer);
        var registry = DeviceTypeRegistry.Instance;
        RegisterBuiltInDeviceTypes(registry);

        PrintSection(writer, "1. Singleton + Factory Registry：创建设备");
        var registryIsStable = ReferenceEquals(registry, DeviceTypeRegistry.Instance);
        writer.WriteLine($"同一注册表实例：{registryIsStable}");
        foreach (var type in registry.RegisteredTypes)
        {
            writer.WriteLine($"  {type.Key,-18} {type.Description}");
        }

        var zigbee = new ZigbeeChannel(journal);
        var wifi = new WifiChannel(journal);
        var hallLightTarget = registry.Create(
            "dimmable-light",
            new DeviceCreationContext("light-hall", "玄关灯", zigbee, journal));
        var bedroomLightTarget = registry.Create(
            "dimmable-light",
            new DeviceCreationContext("light-bedroom", "卧室灯", wifi, journal));
        var sirenTarget = registry.Create(
            "smart-siren",
            new DeviceCreationContext("siren-main", "家庭警报器", wifi, journal));

        PrintSection(writer, "2. Adapter：把旧空调接入统一设备端口");
        var legacyAirConditioner = new LegacyAirConditioner("AC-LEGACY-1998", journal);
        var airConditionerTarget = new LegacyAirConditionerAdapter(
            "ac-living",
            "客厅旧空调",
            legacyAirConditioner,
            journal);

        var policy = new HomeAuthorizationPolicy();
        var audit = new DeviceAuditTrail(journal);
        var owner = new UserIdentity("林女士", HomeRole.Owner);
        var guest = new UserIdentity("访客", HomeRole.Guest);

        var hallLight = Protect(hallLightTarget, owner, policy, audit);
        var bedroomLight = Protect(bedroomLightTarget, owner, policy, audit);
        var siren = Protect(sirenTarget, owner, policy, audit);
        var airConditioner = Protect(airConditionerTarget, owner, policy, audit);
        var guestSiren = Protect(sirenTarget, guest, policy, audit);
        var commandBus = new HomeCommandBus(journal);

        commandBus.Execute(new SetSettingCommand(hallLight, 20));
        commandBus.Execute(new SetPowerCommand(hallLight, turnOn: true));
        commandBus.Execute(new SetSettingCommand(bedroomLight, 45));
        commandBus.Execute(new SetPowerCommand(bedroomLight, turnOn: true));
        commandBus.Execute(new SetSettingCommand(airConditioner, 24));
        commandBus.Execute(new SetPowerCommand(airConditioner, turnOn: true));
        commandBus.Execute(new SetPowerCommand(siren, turnOn: false));
        var adapterConverted = legacyAirConditioner.TemperatureFahrenheit == 75;
        writer.WriteLine($"适配结果：24°C -> {legacyAirConditioner.TemperatureFahrenheit}°F");

        PrintSection(writer, "3. Bridge：同一设备抽象跨 Zigbee / Wi-Fi");
        writer.WriteLine("玄关灯通过 Zigbee，卧室灯与警报器通过 Wi-Fi；设备类没有协议分支。");

        PrintSection(writer, "4. Composite：家庭 / 楼层 / 房间统一操作");
        var home = BuildHomeTree(hallLight, bedroomLight, airConditioner, siren, out var firstFloor);
        home.WriteTree(writer);
        var beforeCompositeOperation = Snapshot(firstFloor.EnumerateDevices());
        writer.WriteLine("对一楼组合节点执行“临时断电”，随后逐条 Undo：");
        firstFloor.ApplyToDevices(device => commandBus.Execute(new SetPowerCommand(device, turnOn: false)));
        for (var index = 0; index < firstFloor.DeviceCount; index++)
        {
            commandBus.UndoLast();
        }

        var compositeRoundTrip = StatesMatch(beforeCompositeOperation, firstFloor.EnumerateDevices());
        writer.WriteLine($"组合操作后撤销，状态完整恢复：{compositeRoundTrip}");

        PrintSection(writer, "5. Proxy：访客不能启动安全关键设备");
        var guestDenied = false;
        try
        {
            commandBus.Execute(new SetPowerCommand(guestSiren, turnOn: true));
        }
        catch (UnauthorizedAccessException exception)
        {
            guestDenied = true;
            writer.WriteLine($"预期拒绝：{exception.Message}");
        }

        PrintSection(writer, "6. Command：调光命令可以 Undo");
        var brightnessBeforeCommand = hallLight.CurrentState.Setting;
        commandBus.Execute(new SetSettingCommand(hallLight, 55));
        writer.WriteLine($"命令后亮度：{hallLight.CurrentState.Setting}%");
        commandBus.UndoLast();
        var commandUndoRestored = hallLight.CurrentState.Setting == brightnessBeforeCommand;
        writer.WriteLine($"Undo 后亮度：{hallLight.CurrentState.Setting}%（恢复={commandUndoRestored}）");

        PrintSection(writer, "7. Mediator：传感器只通知家庭中枢");
        var hub = new HomeHubMediator(commandBus, journal);
        hub.Register(AutomationRole.PathLight, hallLight);
        hub.Register(AutomationRole.Climate, airConditioner);
        hub.Register(AutomationRole.Alarm, siren);
        var entrySensor = new HomeSensor("玄关门磁");
        var smokeSensor = new HomeSensor("厨房烟感");
        entrySensor.Connect(hub);
        smokeSensor.Connect(hub);
        entrySensor.Raise(HomeSignal.EntryOpenedAfterDark);
        var mediatorEntryApplied = hallLight.CurrentState is { IsOn: true, Setting: 35 };
        writer.WriteLine($"夜间入户联动：玄关灯={Describe(hallLight)}");

        PrintSection(writer, "8. Memento：警报前保存，解除后恢复");
        var safetyScene = new SafetySceneOriginator([hallLight, airConditioner, siren], journal);
        var caretaker = new SafetyCheckpointCaretaker(journal);
        var stateBeforeEmergency = Snapshot([hallLight, airConditioner, siren]);
        caretaker.Save(safetyScene.CreateMemento("烟雾警报前"));

        smokeSensor.Raise(HomeSignal.SmokeDetected);
        var emergencyApplied =
            hallLight.CurrentState is { IsOn: true, Setting: 100 } &&
            airConditioner.CurrentState.IsOn is false &&
            siren.CurrentState is { IsOn: true, Setting: 3 };
        writer.WriteLine($"紧急状态：灯={Describe(hallLight)}；空调={Describe(airConditioner)}；警报={Describe(siren)}");

        smokeSensor.Raise(HomeSignal.AlertCleared);
        safetyScene.Restore(caretaker.GetLast());
        var mementoRestored = StatesMatch(stateBeforeEmergency, [hallLight, airConditioner, siren]);
        writer.WriteLine($"恢复结果：{mementoRestored}");
        writer.WriteLine($"恢复状态：灯={Describe(hallLight)}；空调={Describe(airConditioner)}；警报={Describe(siren)}");

        PrintSection(writer, "场景结果");
        var bridgeUsedTwoChannels =
            journal.Entries.Any(entry => entry.Category == "Bridge/Zigbee") &&
            journal.Entries.Any(entry => entry.Category == "Bridge/Wi-Fi");
        var deniedAuditCount = audit.Entries.Count(entry => !entry.Allowed);

        writer.WriteLine($"Singleton 稳定实例：{registryIsStable}");
        writer.WriteLine($"Adapter 温标转换：{adapterConverted}");
        writer.WriteLine($"Bridge 两种通道：{bridgeUsedTwoChannels}");
        writer.WriteLine($"Composite 设备数：{home.DeviceCount}，往返恢复：{compositeRoundTrip}");
        writer.WriteLine($"Proxy 拒绝访客：{guestDenied}，拒绝审计：{deniedAuditCount}");
        writer.WriteLine($"Command Undo：{commandUndoRestored}");
        writer.WriteLine($"Mediator 入户联动：{mediatorEntryApplied}");
        writer.WriteLine($"Mediator 紧急联动：{emergencyApplied}");
        writer.WriteLine($"Memento 恢复：{mementoRestored}");

        return new SmartHomeDemoResult(
            registryIsStable,
            registry.RegisteredTypes.Count,
            adapterConverted,
            bridgeUsedTwoChannels,
            compositeRoundTrip,
            home.DeviceCount,
            guestDenied,
            commandUndoRestored,
            mediatorEntryApplied,
            emergencyApplied,
            mementoRestored,
            audit.Entries.Count,
            deniedAuditCount);
    }

    private static void RegisterBuiltInDeviceTypes(DeviceTypeRegistry registry)
    {
        registry.TryRegister(
            "dimmable-light",
            "可调光灯；通信协议由 Bridge 注入",
            context => new DimmableLight(context.Id, context.DisplayName, context.Channel));
        registry.TryRegister(
            "smart-siren",
            "三级警报器；标记为安全关键设备",
            context => new SmartSiren(context.Id, context.DisplayName, context.Channel));
    }

    private static AuthorizedDeviceProxy Protect(
        ISmartDevice device,
        UserIdentity user,
        IDeviceAuthorizationPolicy policy,
        DeviceAuditTrail audit) =>
        new(device, user, policy, audit);

    private static HomeGroup BuildHomeTree(
        ISmartDevice hallLight,
        ISmartDevice bedroomLight,
        ISmartDevice airConditioner,
        ISmartDevice siren,
        out HomeGroup firstFloor)
    {
        var entry = new HomeGroup("玄关", HomeGroupKind.Room)
            .Add(new DeviceNode(hallLight))
            .Add(new DeviceNode(siren));
        var livingRoom = new HomeGroup("客厅", HomeGroupKind.Room)
            .Add(new DeviceNode(airConditioner));
        firstFloor = new HomeGroup("一楼", HomeGroupKind.Floor)
            .Add(entry)
            .Add(livingRoom);
        var bedroom = new HomeGroup("主卧", HomeGroupKind.Room)
            .Add(new DeviceNode(bedroomLight));
        var secondFloor = new HomeGroup("二楼", HomeGroupKind.Floor)
            .Add(bedroom);
        return new HomeGroup("林家", HomeGroupKind.Home)
            .Add(firstFloor)
            .Add(secondFloor);
    }

    private static IReadOnlyDictionary<string, DeviceState> Snapshot(IEnumerable<ISmartDevice> devices) =>
        devices.ToDictionary(device => device.Id, device => device.CurrentState, StringComparer.Ordinal);

    private static bool StatesMatch(
        IReadOnlyDictionary<string, DeviceState> expected,
        IEnumerable<ISmartDevice> devices) =>
        devices.All(device =>
            expected.TryGetValue(device.Id, out var state) && state == device.CurrentState);

    private static string Describe(ISmartDevice device)
    {
        var state = device.CurrentState;
        return $"{(state.IsOn ? "开" : "关")}/{state.Setting}{device.SettingUnit}";
    }

    private static void PrintSection(TextWriter writer, string title)
    {
        writer.WriteLine();
        writer.WriteLine($"--- {title} ---");
    }
}
