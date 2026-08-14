namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

/// <summary>
/// Flyweight 工厂：同名且配置相同的样式只创建一次。
/// </summary>
public sealed class StyleFlyweightFactory
{
    private readonly Dictionary<string, StyleDefinition> _styles =
        new(StringComparer.OrdinalIgnoreCase);

    public int SharedStyleCount => _styles.Count;

    public StyleDefinition GetOrCreate(
        string name,
        string fontFamily,
        int fontSize,
        string colorHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorHex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);

        if (_styles.TryGetValue(name, out var existing))
        {
            if (!string.Equals(existing.FontFamily, fontFamily, StringComparison.Ordinal) ||
                existing.FontSize != fontSize ||
                !string.Equals(existing.ColorHex, colorHex, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"样式名 '{name}' 已绑定到另一组配置。");
            }

            return existing;
        }

        var style = new StyleDefinition(name, fontFamily, fontSize, colorHex);
        _styles.Add(name, style);
        return style;
    }

    public IReadOnlyList<StyleDefinition> Snapshot() =>
        _styles.Values.OrderBy(style => style.Name, StringComparer.Ordinal).ToArray();
}
