using System.Collections;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

/// <summary>
/// 有明确业务顺序的章节集合，并提供自己的 Iterator 实现。
/// </summary>
public sealed class SectionCollection : IEnumerable<ReportSection>, IPrototype<SectionCollection>
{
    private readonly List<ReportSection> _sections;

    public SectionCollection()
    {
        _sections = [];
    }

    public SectionCollection(IEnumerable<ReportSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        _sections = sections.ToList();
    }

    public int Count => _sections.Count;

    public ReportSection this[int index] => _sections[index];

    public void Add(ReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (_sections.Any(existing => string.Equals(existing.Id, section.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"章节 ID '{section.Id}' 已存在。");
        }

        _sections.Add(section);
    }

    public SectionCollection DeepClone() =>
        new(_sections.Select(section => section.DeepClone()));

    public SectionCollection SelectMatching(Func<ReportSection, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new SectionCollection(_sections.Where(predicate));
    }

    public SectionEnumerator GetEnumerator() => new(_sections);

    IEnumerator<ReportSection> IEnumerable<ReportSection>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public sealed class SectionEnumerator : IEnumerator<ReportSection>
    {
        private readonly IReadOnlyList<ReportSection> _sections;
        private int _index = -1;

        internal SectionEnumerator(IReadOnlyList<ReportSection> sections)
        {
            _sections = sections;
        }

        public ReportSection Current =>
            _index >= 0 && _index < _sections.Count
                ? _sections[_index]
                : throw new InvalidOperationException("迭代器当前没有指向有效章节。");

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index + 1 >= _sections.Count)
            {
                _index = _sections.Count;
                return false;
            }

            _index++;
            return true;
        }

        public void Reset() => _index = -1;

        public void Dispose()
        {
        }
    }
}
