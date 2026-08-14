using DesignPatterns.TeachingProjects.SmartHome.Demo;

namespace DesignPatterns.TeachingProjects.SmartHome.Testing;

public static class SelfTestRunner
{
    public static int Run(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("SmartHome self-test (.NET 10, no third-party test framework)");

        SmartHomeDemoResult result;
        try
        {
            result = SmartHomeDemo.Run(TextWriter.Null);
        }
        catch (Exception exception)
        {
            writer.WriteLine($"[FAIL] 场景执行抛出异常：{exception}");
            return 1;
        }

        var checks = new (string Name, bool Passed)[]
        {
            ("Singleton 返回稳定实例", result.SingletonIsStable),
            ("注册表至少包含两种可创建设备", result.RegisteredDeviceTypeCount >= 2),
            ("Adapter 把 24°C 转成旧接口的 75°F", result.AdapterConvertedCelsius),
            ("Bridge 同时使用 Zigbee 与 Wi-Fi", result.BridgeUsedTwoChannels),
            ("Composite 统一遍历四台设备", result.CompositeDeviceCount == 4),
            ("Composite 批量操作可借助 Command 完整撤销", result.CompositeRoundTripRestored),
            ("Protection Proxy 拒绝访客启动警报", result.ProxyDeniedGuest),
            ("拒绝事件进入审计", result.DeniedAuditEntryCount == 1),
            ("Command Undo 恢复原亮度", result.CommandUndoRestored),
            ("Mediator 执行夜间入户联动", result.MediatorEntryRuleApplied),
            ("Mediator 执行烟雾紧急联动", result.EmergencyRuleApplied),
            ("Memento 恢复警报前的三台设备", result.MementoRestored),
            ("Proxy 对允许和拒绝操作均有审计", result.AuditEntryCount > result.DeniedAuditEntryCount)
        };

        foreach (var check in checks)
        {
            writer.WriteLine($"[{(check.Passed ? "PASS" : "FAIL")}] {check.Name}");
        }

        var failed = checks.Count(check => !check.Passed);
        writer.WriteLine($"结果：{checks.Length - failed}/{checks.Length} 通过");
        return failed == 0 ? 0 : 1;
    }
}
