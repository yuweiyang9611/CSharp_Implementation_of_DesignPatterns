namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed record OrderLine(string Sku, string ProductName, decimal UnitPrice, int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
