using System.Collections.Concurrent;

namespace ReliableCheckout.Infrastructure;

public interface IFailureInjector
{
    void FailNext(string point, int count = 1);

    void ThrowIfScheduled(string point);
}

public sealed class InjectedFailureException(string point)
    : Exception($"A deterministic failure was injected at '{point}'.");

/// <summary>
/// Tests schedule failures by name; production remains failure-free unless explicitly configured.
/// </summary>
public sealed class DeterministicFailureInjector : IFailureInjector
{
    private readonly ConcurrentDictionary<string, int> remaining = new(StringComparer.Ordinal);

    public void FailNext(string point, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        remaining.AddOrUpdate(point, count, (_, _) => count);
    }

    public void ThrowIfScheduled(string point)
    {
        while (remaining.TryGetValue(point, out var count) && count > 0)
        {
            if (!remaining.TryUpdate(point, count - 1, count))
            {
                continue;
            }

            throw new InjectedFailureException(point);
        }
    }
}
