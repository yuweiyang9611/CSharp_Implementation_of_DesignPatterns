namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed class Product
{
    public Product(string sku, string name, decimal unitPrice, int availableStock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (unitPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "商品单价不能为负数。");
        }

        if (availableStock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableStock), "可用库存不能为负数。");
        }

        Sku = sku;
        Name = name;
        UnitPrice = unitPrice;
        AvailableStock = availableStock;
    }

    public string Sku { get; }

    public string Name { get; }

    public decimal UnitPrice { get; }

    public int AvailableStock { get; private set; }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "预留数量必须大于零。");
        }

        if (quantity > AvailableStock)
        {
            throw new InvalidOperationException($"商品 {Sku} 库存不足。");
        }

        AvailableStock -= quantity;
    }
}
