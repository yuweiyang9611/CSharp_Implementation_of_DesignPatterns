namespace ReliableCheckout.Infrastructure;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// A controllable clock makes retry tests instant and deterministic.
/// </summary>
public sealed class ManualClock(DateTimeOffset initialValue) : IClock
{
    private readonly object sync = new();
    private DateTimeOffset value = initialValue;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (sync)
            {
                return value;
            }
        }
    }

    public void Advance(TimeSpan duration)
    {
        lock (sync)
        {
            value = value.Add(duration);
        }
    }
}
