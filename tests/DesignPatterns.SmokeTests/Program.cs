using System.Text;
using DesignPatterns;

Console.OutputEncoding = Encoding.UTF8;
var failures = new List<string>();

Check(PatternCatalog.All.Count == 23, $"应有 23 个模式，实际为 {PatternCatalog.All.Count}。", failures);
Check(PatternCatalog.All.Select(demo => demo.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 23, "模式 Key 必须唯一。", failures);
Check(PatternCatalog.All.Count(demo => demo.Category == "Creational") == 5, "创建型模式应有 5 个。", failures);
Check(PatternCatalog.All.Count(demo => demo.Category == "Structural") == 7, "结构型模式应有 7 个。", failures);
Check(PatternCatalog.All.Count(demo => demo.Category == "Behavioral") == 11, "行为型模式应有 11 个。", failures);

foreach (var demo in PatternCatalog.All)
{
    try
    {
        Check(!string.IsNullOrWhiteSpace(demo.Name), $"{demo.Key}: Name 不能为空。", failures);
        Check(!string.IsNullOrWhiteSpace(demo.Intent), $"{demo.Key}: Intent 不能为空。", failures);

        var firstRun = demo.Run();
        var secondRun = demo.Run();
        Check(firstRun.Count >= 2, $"{demo.Key}: 示例至少应产生两行可观察输出。", failures);
        Check(firstRun.All(line => !string.IsNullOrWhiteSpace(line)), $"{demo.Key}: 输出不应包含空行。", failures);
        Check(firstRun.SequenceEqual(secondRun, StringComparer.Ordinal), $"{demo.Key}: 重复运行应产生确定性输出。", failures);
    }
    catch (Exception exception)
    {
        failures.Add($"{demo.Key}: 运行时抛出 {exception.GetType().Name}: {exception.Message}");
    }
}
if (failures.Count > 0)
{
    Console.Error.WriteLine($"烟雾测试失败（{failures.Count} 项）：");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine($"烟雾测试通过：{PatternCatalog.All.Count} 个模式均可重复运行，Key、分类与输出均符合约定。");
return 0;

static void Check(bool condition, string failure, ICollection<string> failures)
{
    if (!condition)
    {
        failures.Add(failure);
    }
}
