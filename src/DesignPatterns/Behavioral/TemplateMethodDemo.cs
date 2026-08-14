namespace DesignPatterns.Behavioral;

/// <summary>
/// Defines a stable export pipeline while formats customize selected steps.
/// </summary>
public sealed class TemplateMethodDemo : IPatternDemo
{
    public string Key => "template-method";

    public string Name => "Template Method / 模板方法模式";

    public string Category => "Behavioral";

    public string Intent => "定义算法骨架，并允许子类改写其中的特定步骤。";

    public IReadOnlyList<string> Run()
    {
        IReadOnlyList<OrderRow> orders =
        [
            new("ORD-2", "Ben", 80m),
            new("ORD-1", "Aiko", 120m)
        ];

        var output = new List<string>();
        output.AddRange(new CsvOrderExport().Export(orders));
        output.AddRange(new JsonOrderExport().Export(orders));
        return output;
    }

    private sealed record OrderRow(string Id, string Customer, decimal Total);

    // Abstract class: Export is the template method; its sequence is deliberately nonvirtual.
    private abstract class OrderExportTemplate
    {
        protected abstract string FormatName { get; }

        protected abstract string Destination { get; }

        internal IReadOnlyList<string> Export(IReadOnlyList<OrderRow> orders)
        {
            Validate(orders);
            var normalized = Normalize(orders);
            var output = new List<string>
            {
                $"{FormatName}: validated {orders.Count} orders.",
                $"{FormatName}: normalized order is {string.Join(", ", normalized.Select(row => row.Id))}.",
                $"{FormatName}: {Format(normalized)}"
            };

            if (ShouldCompress)
            {
                output.Add($"{FormatName}: compressed the payload.");
            }

            output.Add($"{FormatName}: delivered to {Destination}.");
            return output;
        }

        protected abstract string Format(IReadOnlyList<OrderRow> orders);

        protected virtual bool ShouldCompress => false;

        private static void Validate(IReadOnlyCollection<OrderRow> orders)
        {
            if (orders.Count == 0 || orders.Any(order => order.Total < 0m))
            {
                throw new InvalidOperationException("Orders must be nonempty and have nonnegative totals.");
            }
        }

        private static IReadOnlyList<OrderRow> Normalize(IEnumerable<OrderRow> orders) =>
            orders.OrderBy(order => order.Id, StringComparer.Ordinal).ToArray();
    }

    private sealed class CsvOrderExport : OrderExportTemplate
    {
        protected override string FormatName => "CSV";

        protected override string Destination => "finance/orders.csv";

        protected override string Format(IReadOnlyList<OrderRow> orders) =>
            $"formatted header Id,Customer,Total plus {orders.Count} data rows.";
    }

    private sealed class JsonOrderExport : OrderExportTemplate
    {
        protected override string FormatName => "JSON";

        protected override string Destination => "analytics/orders.json.gz";

        protected override bool ShouldCompress => true;

        protected override string Format(IReadOnlyList<OrderRow> orders) =>
            $"formatted an array with {orders.Count} objects.";
    }
}
