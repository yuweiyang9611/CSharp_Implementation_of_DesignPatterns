namespace DesignPatterns;

/// <summary>
/// A small, executable scenario that demonstrates one Gang of Four pattern.
/// </summary>
public interface IPatternDemo
{
    string Key { get; }

    string Name { get; }

    string Category { get; }

    string Intent { get; }

    IReadOnlyList<string> Run();
}
