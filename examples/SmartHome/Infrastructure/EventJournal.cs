namespace DesignPatterns.TeachingProjects.SmartHome.Infrastructure;

/// <summary>用递增序号代替时间戳，使示例输出可重复、可测试。</summary>
public sealed class EventJournal
{
    private readonly TextWriter _writer;
    private readonly List<JournalEntry> _entries = [];

    public EventJournal(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public IReadOnlyList<JournalEntry> Entries => _entries;

    public void Record(string category, string message)
    {
        var entry = new JournalEntry(_entries.Count + 1, category, message);
        _entries.Add(entry);
        _writer.WriteLine($"[{entry.Sequence:00}][{entry.Category}] {entry.Message}");
    }
}

public sealed record JournalEntry(int Sequence, string Category, string Message);
