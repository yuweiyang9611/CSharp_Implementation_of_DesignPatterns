using DesignPatterns;

namespace DesignPatterns.TeachingProjects.Tests;

public sealed class PatternCatalogTests
{
    public static TheoryData<string, string> SemanticCases =>
        new()
        {
            { "iterator", "Favorite tracks:" },
            { "adapter", "适配后的读数: 5.0 °C" },
            { "template-method", "JSON: compressed the payload." },
            { "factory-method", "预计送达: 1 天" },
            { "singleton", "两个调用者持有同一实例: True" },
            { "prototype", "渠道集合已深复制: True" },
            { "builder", "需要人工审批: True" },
            { "abstract-factory", "[TouchButton caption='提交订单' min-height='48']" },
            { "bridge", "新增告警类型无需修改渠道；新增渠道也无需修改告警类型。" },
            { "strategy", "Express courier: cost 25.50, delivery in 1 day(s)." },
            { "composite", "组合节点自动汇总总工时: 22h" },
            { "decorator", "加税后应付: CNY 415.80" },
            { "visitor", "Shipping total: 9.80." },
            { "chain-of-responsibility", "Finance director rejected New office lease" },
            { "facade", "订单 SO-2048 结算完成" },
            { "mediator", "Tower cleared SQ303 to land." },
            { "observer", "Email observer detached." },
            { "memento", "Second undo restored: title='Design Notes', content='Draft'." },
            { "state", "ORD-200: cannot pay a cancelled order." },
            { "flyweight", "3 个地图标记只创建了 2 个样式对象" },
            { "proxy", "远程服务实际调用 2 次，缓存命中 1 次" },
            { "command", "Redid replace 'Design' with 'GoF Design'" },
            { "interpreter", "Ben: denied" },
        };

    [Fact]
    public void Catalog_HasTheCompleteGofDistributionWithUniqueKeys()
    {
        Assert.Equal(23, PatternCatalog.All.Count);
        Assert.Equal(
            23,
            PatternCatalog.All.Select(demo => demo.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(5, PatternCatalog.All.Count(demo => demo.Category == "Creational"));
        Assert.Equal(7, PatternCatalog.All.Count(demo => demo.Category == "Structural"));
        Assert.Equal(11, PatternCatalog.All.Count(demo => demo.Category == "Behavioral"));
    }

    [Fact]
    public void Find_IsCaseInsensitiveAndReturnsNullForUnknownKey()
    {
        Assert.Equal("adapter", PatternCatalog.Find("ADAPTER")?.Key);
        Assert.Null(PatternCatalog.Find("not-a-pattern"));
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public void Demo_ProducesDeterministicOutputWithExpectedBusinessResult(string key, string expectedFragment)
    {
        IPatternDemo demo = Assert.IsAssignableFrom<IPatternDemo>(PatternCatalog.Find(key));

        IReadOnlyList<string> firstRun = demo.Run();
        IReadOnlyList<string> secondRun = demo.Run();

        Assert.NotEmpty(firstRun);
        Assert.DoesNotContain(firstRun, string.IsNullOrWhiteSpace);
        Assert.Equal(firstRun, secondRun);
        Assert.Contains(firstRun, line => line.Contains(expectedFragment, StringComparison.Ordinal));
    }
}
