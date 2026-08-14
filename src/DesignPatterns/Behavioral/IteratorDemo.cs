using System.Collections;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Traverses a playlist in useful orders without exposing its internal collection.
/// </summary>
public sealed class IteratorDemo : IPatternDemo
{
    public string Key => "iterator";

    public string Name => "Iterator / 迭代器模式";

    public string Category => "Behavioral";

    public string Intent => "在不暴露集合内部结构的前提下顺序访问元素。";

    public IReadOnlyList<string> Run()
    {
        var playlist = new Playlist();
        playlist.Add(new Track(2, "Patterns in Practice", TimeSpan.FromSeconds(245), true));
        playlist.Add(new Track(1, "Object Overture", TimeSpan.FromSeconds(150), false));
        playlist.Add(new Track(3, "Refactoring Rhythm", TimeSpan.FromSeconds(198), true));

        var output = new List<string> { "Play order:" };
        foreach (var track in playlist)
        {
            output.Add($"{track.Position}. {track.Title} ({track.Duration:mm\\:ss})");
        }

        output.Add("Favorite tracks:");
        foreach (var track in playlist.Favorites())
        {
            output.Add(track.Title);
        }

        return output;
    }

    private sealed record Track(int Position, string Title, TimeSpan Duration, bool IsFavorite);

    // Aggregate: callers can use foreach but cannot mutate or depend on the backing list.
    private sealed class Playlist : IEnumerable<Track>
    {
        private readonly List<Track> _tracks = new();

        internal void Add(Track track) => _tracks.Add(track);

        public IEnumerator<Track> GetEnumerator()
        {
            foreach (var track in _tracks.OrderBy(track => track.Position))
            {
                yield return track;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Iterator blocks make alternative traversals lazy and keep collection details private.
        internal IEnumerable<Track> Favorites()
        {
            foreach (var track in _tracks.Where(track => track.IsFavorite).OrderBy(track => track.Position))
            {
                yield return track;
            }
        }
    }
}
