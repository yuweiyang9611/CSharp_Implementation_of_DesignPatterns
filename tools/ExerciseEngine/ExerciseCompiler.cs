using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExerciseEngine;

public sealed record CompilerDiagnostic(string Message, string Severity, int Line, int Column);
public sealed record CompilationResult(bool Success, CompilerDiagnostic[] Diagnostics);
public sealed record CheckResult(string Name, bool Passed);
public sealed record ExecutionResult(CheckResult[] Checks, string Output, bool OutputTruncated, string? Error);

public sealed class ExerciseCompiler
{
    private readonly List<MetadataReference> references = [];
    private byte[]? image;
    public void AddReference(byte[] bytes) => references.Add(MetadataReference.CreateFromImage(bytes));
    public CompilationResult Compile(string source, string checks)
    {
        image = null;
        if (Encoding.UTF8.GetByteCount(source) > 100 * 1024)
            return new(false, [new("源码不能超过 100 KB。", "Error", 0, 0)]);
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var compilation = CSharpCompilation.Create("Submission_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, options, "Submission.cs"), CSharpSyntaxTree.ParseText(checks, options, "Checks.cs")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false, concurrentBuild: false));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        var diagnostics = result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(diagnostic =>
            {
                var position = diagnostic.Location.GetLineSpan().StartLinePosition;
                return new CompilerDiagnostic(diagnostic.GetMessage(), diagnostic.Severity.ToString(), position.Line + 1, position.Character + 1);
            }).ToArray();
        if (result.Success) image = stream.ToArray();
        return new(result.Success, diagnostics);
    }
    public ExecutionResult Execute()
    {
        if (image is null) return new([], "", false, "请先通过编译。");
        var output = new BoundedOutput();
        var previousOut = Console.Out;
        var previousError = Console.Error;
        try
        {
            Console.SetOut(output); Console.SetError(output);
            var assembly = Assembly.Load(image);
            var method = assembly.GetType("ExerciseChecks")?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("验收入口 ExerciseChecks.Run 不存在。");
            var results = (string[])(method.Invoke(null, null) ?? throw new InvalidOperationException("验收没有返回结果。"));
            return new(results.Select(result => new CheckResult(result[1..], result.StartsWith('+'))).ToArray(), output.ToString(), output.Truncated, null);
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException invocation ? invocation.InnerException ?? exception : exception;
            return new([], output.ToString(), output.Truncated, $"{cause.GetType().Name}: {cause.Message}");
        }
        finally { Console.SetOut(previousOut); Console.SetError(previousError); image = null; }
    }
    private sealed class BoundedOutput : TextWriter
    {
        private readonly StringBuilder text = new();
        private int bytes;
        public bool Truncated { get; private set; }
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) => Write(value.ToString());
        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (var rune in value.EnumerateRunes())
            {
                if (bytes + rune.Utf8SequenceLength > 64 * 1024) { Truncated = true; break; }
                bytes += rune.Utf8SequenceLength; text.Append(rune.ToString());
            }
        }
        public override string ToString() => text.ToString();
    }
}
