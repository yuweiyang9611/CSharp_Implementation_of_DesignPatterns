namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates treating individual tasks and nested work groups through one work-item interface.
/// </summary>
public sealed class CompositeDemo : IPatternDemo
{
    public string Key => "composite";

    public string Name => "Composite / 组合模式";

    public string Category => "Structural";

    public string Intent => "把对象组织成树形结构，使客户端能一致地处理叶节点和组合节点。";

    public IReadOnlyList<string> Run()
    {
        var paymentFeature = new WorkGroup("支付功能")
            .Add(new TaskItem("接入支付网关", 8))
            .Add(new TaskItem("处理回调签名", 5));

        var release = new WorkGroup("Release 2.0")
            .Add(new TaskItem("更新数据库架构", 3))
            .Add(paymentFeature)
            .Add(new TaskItem("端到端回归测试", 6));

        return
        [
            .. release.Render(depth: 0),
            $"组合节点自动汇总总工时: {release.EstimateHours}h"
        ];
    }

    // Component: both leaves and composites expose the same operations.
    private interface IWorkItem
    {
        string Name { get; }

        int EstimateHours { get; }

        IEnumerable<string> Render(int depth);
    }

    private sealed class TaskItem(string name, int estimateHours) : IWorkItem
    {
        public string Name { get; } = name;

        public int EstimateHours { get; } = estimateHours;

        public IEnumerable<string> Render(int depth)
        {
            yield return $"{Indent(depth)}- {Name} ({EstimateHours}h)";
        }
    }

    private sealed class WorkGroup(string name) : IWorkItem
    {
        private readonly List<IWorkItem> _children = [];

        public string Name { get; } = name;

        public int EstimateHours => _children.Sum(child => child.EstimateHours);

        public WorkGroup Add(IWorkItem child)
        {
            ArgumentNullException.ThrowIfNull(child);
            _children.Add(child);
            return this;
        }

        public IEnumerable<string> Render(int depth)
        {
            yield return $"{Indent(depth)}+ {Name} ({EstimateHours}h)";

            foreach (var child in _children)
            {
                foreach (var line in child.Render(depth + 1))
                {
                    yield return line;
                }
            }
        }
    }

    private static string Indent(int depth) => new(' ', depth * 2);
}
