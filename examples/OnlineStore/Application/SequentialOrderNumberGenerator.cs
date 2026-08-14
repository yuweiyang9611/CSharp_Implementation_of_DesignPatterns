namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public interface IOrderNumberGenerator
{
    string Next();
}

public sealed class SequentialOrderNumberGenerator(string prefix = "ORD-20260715") : IOrderNumberGenerator
{
    private int _sequence;

    public string Next()
    {
        _sequence++;
        return $"{prefix}-{_sequence:0000}";
    }
}
