namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed class ProductCatalog(IEnumerable<Product> products)
{
    private readonly Dictionary<string, Product> _products = products.ToDictionary(
        product => product.Sku,
        StringComparer.OrdinalIgnoreCase);

    public Product GetRequired(string sku)
    {
        return _products.TryGetValue(sku, out Product? product)
            ? product
            : throw new KeyNotFoundException($"找不到商品 {sku}。");
    }

    public bool HasStock(string sku, int quantity)
    {
        return _products.TryGetValue(sku, out Product? product) &&
               quantity > 0 &&
               product.AvailableStock >= quantity;
    }

    public void Reserve(IReadOnlyList<OrderLine> lines)
    {
        var requestedBySku = lines
            .GroupBy(line => line.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Sku = group.Key,
                Quantity = group.Aggregate(0, (total, line) => checked(total + line.Quantity)),
            })
            .ToArray();

        if (requestedBySku.Any(request => !HasStock(request.Sku, request.Quantity)))
        {
            throw new InvalidOperationException("预留库存时发现库存不足，未执行扣减。");
        }

        foreach (var request in requestedBySku)
        {
            GetRequired(request.Sku).Reserve(request.Quantity);
        }
    }

    public static ProductCatalog CreateDemoCatalog()
    {
        return new ProductCatalog(
        [
            new Product("BOOK-DP-CS", "《C# 设计模式实战》", 128m, 8),
            new Product("MUG-CSHARP", "C# 马克杯", 79m, 12),
            new Product("KEYBOARD-75", "75% 机械键盘", 499m, 3),
        ]);
    }
}
