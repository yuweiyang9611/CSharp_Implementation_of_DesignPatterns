using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using ExerciseEngine;

[SupportedOSPlatform("browser")]
public static partial class BrowserCompiler
{
    private static readonly ExerciseCompiler Compiler = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static void Main() { }
    [JSExport] public static void AddReference(byte[] bytes) => Compiler.AddReference(bytes);
    [JSExport] public static string Compile(string source, string checks) => JsonSerializer.Serialize(Compiler.Compile(source, checks), Json);
    [JSExport] public static string Execute() => JsonSerializer.Serialize(Compiler.Execute(), Json);
}
