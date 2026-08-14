using System.Globalization;

namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates sharing immutable marker styles while keeping coordinates and names outside the flyweight.
/// </summary>
public sealed class FlyweightDemo : IPatternDemo
{
    public string Key => "flyweight";

    public string Name => "Flyweight / 享元模式";

    public string Category => "Structural";

    public string Intent => "共享细粒度对象的内在状态，以较小内存代价表示大量对象。";

    public IReadOnlyList<string> Run()
    {
        var styles = new MarkerStyleFactory();
        var firstCafeStyle = styles.Get(MarkerKind.Cafe);
        var secondCafeStyle = styles.Get(MarkerKind.Cafe);

        MapMarker[] markers =
        [
            new("Kanda Coffee", 35.691m, 139.771m, firstCafeStyle),
            new("River Cafe", 35.696m, 139.775m, secondCafeStyle),
            new("Akihabara Station", 35.698m, 139.774m, styles.Get(MarkerKind.Station))
        ];

        return
        [
            .. markers.Select(marker => marker.Render()),
            $"两个咖啡店共享同一样式实例: {ReferenceEquals(firstCafeStyle, secondCafeStyle)}",
            $"3 个地图标记只创建了 {styles.CachedStyleCount} 个样式对象"
        ];
    }

    private enum MarkerKind
    {
        Cafe,
        Station
    }

    // Flyweight contains only immutable intrinsic state that can safely be shared.
    private sealed record MarkerStyle(string Icon, string Color);

    // Context owns extrinsic state (name and coordinates) and supplies it when rendering.
    private sealed record MapMarker(
        string Name,
        decimal Latitude,
        decimal Longitude,
        MarkerStyle Style)
    {
        public string Render() => string.Create(
            CultureInfo.InvariantCulture,
            $"{Style.Icon} {Name} @ ({Latitude:0.000}, {Longitude:0.000}), color={Style.Color}");
    }

    private sealed class MarkerStyleFactory
    {
        private readonly Dictionary<MarkerKind, MarkerStyle> _cache = [];

        public int CachedStyleCount => _cache.Count;

        public MarkerStyle Get(MarkerKind kind)
        {
            if (_cache.TryGetValue(kind, out var existing))
            {
                return existing;
            }

            var created = kind switch
            {
                MarkerKind.Cafe => new MarkerStyle("CAFE", "brown"),
                MarkerKind.Station => new MarkerStyle("TRAIN", "blue"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown marker kind.")
            };

            _cache.Add(kind, created);
            return created;
        }
    }
}
