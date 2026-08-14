namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates a virtual/caching proxy that controls access to a simulated remote catalog.
/// </summary>
public sealed class ProxyDemo : IPatternDemo
{
    public string Key => "proxy";

    public string Name => "Proxy / 代理模式";

    public string Category => "Structural";

    public string Intent => "为另一个对象提供替身，以控制访问并附加延迟加载、缓存或权限检查。";

    public IReadOnlyList<string> Run()
    {
        var remoteCatalog = new RemoteProductCatalog();
        var cachingProxy = new CachingCatalogProxy(remoteCatalog);
        IProductCatalog catalog = cachingProxy;

        var firstKeyboardLookup = catalog.FindBySku("KB-100");
        var secondKeyboardLookup = catalog.FindBySku("kb-100");
        var monitorLookup = catalog.FindBySku("MON-27");

        return
        [
            $"首次查询: {firstKeyboardLookup.Sku} / {firstKeyboardLookup.Name}",
            $"重复查询: {secondKeyboardLookup.Sku} / {secondKeyboardLookup.Name}",
            $"另一商品: {monitorLookup.Sku} / {monitorLookup.Name}",
            $"重复查询返回缓存中的同一对象: {ReferenceEquals(firstKeyboardLookup, secondKeyboardLookup)}",
            $"客户端查询 3 次，远程服务实际调用 {remoteCatalog.CallCount} 次，缓存命中 {cachingProxy.CacheHitCount} 次"
        ];
    }

    private sealed record Product(string Sku, string Name);

    // Subject: real service and proxy share this contract, so clients can use either transparently.
    private interface IProductCatalog
    {
        Product FindBySku(string sku);
    }

    private sealed class RemoteProductCatalog : IProductCatalog
    {
        public int CallCount { get; private set; }

        public Product FindBySku(string sku)
        {
            CallCount++;

            return sku.ToUpperInvariant() switch
            {
                "KB-100" => new Product("KB-100", "Mechanical Keyboard"),
                "MON-27" => new Product("MON-27", "27-inch Monitor"),
                _ => throw new KeyNotFoundException($"Unknown SKU: {sku}")
            };
        }
    }

    // Proxy adds caching; in a real system it must also define a staleness/invalidation policy.
    private sealed class CachingCatalogProxy(IProductCatalog remote) : IProductCatalog
    {
        private readonly Dictionary<string, Product> _cache = new(StringComparer.OrdinalIgnoreCase);

        public int CacheHitCount { get; private set; }

        public Product FindBySku(string sku)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sku);

            if (_cache.TryGetValue(sku, out var cached))
            {
                CacheHitCount++;
                return cached;
            }

            var product = remote.FindBySku(sku);
            _cache.Add(product.Sku, product);
            return product;
        }
    }
}
