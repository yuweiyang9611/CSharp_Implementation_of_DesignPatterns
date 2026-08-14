namespace DesignPatterns.Behavioral;

/// <summary>
/// Captures editor snapshots so a caretaker can restore state without understanding it.
/// </summary>
public sealed class MementoDemo : IPatternDemo
{
    public string Key => "memento";

    public string Name => "Memento / 备忘录模式";

    public string Category => "Behavioral";

    public string Intent => "在不暴露内部实现的前提下捕获并恢复对象状态。";

    public IReadOnlyList<string> Run()
    {
        var output = new List<string>();
        var editor = new TextEditor("Design Notes", "Draft");
        var history = new EditorHistory();

        history.Backup(editor);
        output.Add($"Saved version 1: {editor.Describe()}.");

        editor.Edit("Patterns explained with examples");
        history.Backup(editor);
        output.Add($"Saved version 2: {editor.Describe()}.");

        editor.Rename("Untitled");
        editor.Edit(string.Empty);
        output.Add($"Accidental edit: {editor.Describe()}.");

        history.Undo(editor);
        output.Add($"Undo restored: {editor.Describe()}.");
        history.Undo(editor);
        output.Add($"Second undo restored: {editor.Describe()}.");

        return output;
    }

    // Marker contract keeps the caretaker from depending on snapshot fields.
    private interface IEditorMemento
    {
    }

    // Originator: only the editor creates and interprets its snapshots.
    private sealed class TextEditor
    {
        private string _title;
        private string _content;

        internal TextEditor(string title, string content)
        {
            _title = title;
            _content = content;
        }

        internal void Rename(string title) => _title = title;

        internal void Edit(string content) => _content = content;

        internal IEditorMemento Save() => new Snapshot(_title, _content);

        internal void Restore(IEditorMemento memento)
        {
            if (memento is not Snapshot snapshot)
            {
                throw new ArgumentException("The snapshot was not created by this editor.", nameof(memento));
            }

            _title = snapshot.Title;
            _content = snapshot.Content;
        }

        internal string Describe() => $"title='{_title}', content='{_content}'";

        private sealed record Snapshot(string Title, string Content) : IEditorMemento;
    }

    // Caretaker: it stores opaque mementos and never reads editor internals.
    private sealed class EditorHistory
    {
        private readonly Stack<IEditorMemento> _snapshots = new();

        internal void Backup(TextEditor editor) => _snapshots.Push(editor.Save());

        internal void Undo(TextEditor editor) => editor.Restore(_snapshots.Pop());
    }
}
