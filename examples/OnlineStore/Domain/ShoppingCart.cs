namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed record CartItem(Product Product, int Quantity);

public sealed class ShoppingCart
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items;

    public void Add(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "加入购物车的数量必须大于零。");
        }

        int existingIndex = _items.FindIndex(item =>
            item.Product.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase));
        if (existingIndex < 0)
        {
            _items.Add(new CartItem(product, quantity));
            return;
        }

        CartItem existing = _items[existingIndex];
        _items[existingIndex] = existing with
        {
            Quantity = checked(existing.Quantity + quantity),
        };
    }
}
