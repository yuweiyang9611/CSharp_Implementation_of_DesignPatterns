namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public interface ICheckoutTrace
{
    IReadOnlyList<string> Entries { get; }

    void Add(string message);
}

public sealed class CheckoutTrace(bool echo) : ICheckoutTrace
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string message)
    {
        _entries.Add(message);
        if (echo)
        {
            Console.WriteLine(message);
        }
    }
}
