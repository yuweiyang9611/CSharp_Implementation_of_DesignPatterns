namespace DesignPatterns.Behavioral;

/// <summary>
/// Turns editor operations into objects so an invoker can execute, undo, and redo them.
/// </summary>
public sealed class CommandDemo : IPatternDemo
{
    public string Key => "command";

    public string Name => "Command / 命令模式";

    public string Category => "Behavioral";

    public string Intent => "把请求封装为对象，从而支持操作历史、撤销与重做。";

    public IReadOnlyList<string> Run()
    {
        var output = new List<string>();
        var document = new TextDocument();
        var history = new CommandHistory(output);

        history.Execute(new AppendTextCommand(document, "Design"));
        history.Execute(new AppendTextCommand(document, " Patterns"));
        history.Execute(new ReplaceTextCommand(document, "Design", "GoF Design"));
        history.Undo();
        history.Redo();

        return output;
    }

    // Command objects know how to reverse their own changes to the receiver.
    private interface IEditorCommand
    {
        string Description { get; }

        string Result { get; }

        void Execute();

        void Undo();
    }

    // Receiver: it contains the actual text-editing behavior.
    private sealed class TextDocument
    {
        internal string Text { get; set; } = string.Empty;
    }

    private sealed class AppendTextCommand : IEditorCommand
    {
        private readonly TextDocument _document;
        private readonly string _text;
        private string _before = string.Empty;

        internal AppendTextCommand(TextDocument document, string text)
        {
            _document = document;
            _text = text;
        }

        public string Description => $"append '{_text}'";

        public string Result => _document.Text;

        public void Execute()
        {
            _before = _document.Text;
            _document.Text += _text;
        }

        public void Undo() => _document.Text = _before;
    }

    private sealed class ReplaceTextCommand : IEditorCommand
    {
        private readonly TextDocument _document;
        private readonly string _oldValue;
        private readonly string _newValue;
        private string _before = string.Empty;

        internal ReplaceTextCommand(TextDocument document, string oldValue, string newValue)
        {
            _document = document;
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public string Description => $"replace '{_oldValue}' with '{_newValue}'";

        public string Result => _document.Text;

        public void Execute()
        {
            _before = _document.Text;
            _document.Text = _document.Text.Replace(
                _oldValue,
                _newValue,
                StringComparison.Ordinal);
        }

        public void Undo() => _document.Text = _before;
    }

    // Invoker: history is reusable because it depends only on the command interface.
    private sealed class CommandHistory
    {
        private readonly Stack<IEditorCommand> _undo = new();
        private readonly Stack<IEditorCommand> _redo = new();
        private readonly ICollection<string> _output;

        internal CommandHistory(ICollection<string> output)
        {
            _output = output;
        }

        internal void Execute(IEditorCommand command)
        {
            command.Execute();
            _undo.Push(command);
            _redo.Clear();
            _output.Add($"Executed {command.Description}; document = '{command.Result}'.");
        }

        internal void Undo()
        {
            var command = _undo.Pop();
            command.Undo();
            _redo.Push(command);
            _output.Add($"Undid {command.Description}; document = '{command.Result}'.");
        }

        internal void Redo()
        {
            var command = _redo.Pop();
            command.Execute();
            _undo.Push(command);
            _output.Add($"Redid {command.Description}; document = '{command.Result}'.");
        }
    }
}
