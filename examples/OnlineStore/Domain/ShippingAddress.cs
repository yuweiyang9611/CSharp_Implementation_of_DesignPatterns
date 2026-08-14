namespace DesignPatterns.TeachingProjects.OnlineStore.Domain;

public sealed record ShippingAddress(
    string Prefecture,
    string City,
    string Street,
    string PostalCode)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Prefecture) &&
        !string.IsNullOrWhiteSpace(City) &&
        !string.IsNullOrWhiteSpace(Street) &&
        !string.IsNullOrWhiteSpace(PostalCode);

    public override string ToString() => $"〒{PostalCode} {Prefecture}{City}{Street}";
}
