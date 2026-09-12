using System.Text.Json;
using ExerciseEngine;

namespace DesignPatterns.GuideExporter.Tests;

[CollectionDefinition("Console compiler", DisableParallelization = true)]
public sealed class CompilerCollection;

[Collection("Console compiler")]
public sealed class ExerciseTests
{
    public static IEnumerable<object[]> Exercises => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "coding-exercises.json")))
        .RootElement.GetProperty("exercises").EnumerateArray().Select(item => new object[] { item.GetProperty("id").GetString()!, item.GetRawText() });
    private static ExerciseCompiler Compiler()
    {
        var compiler = new ExerciseCompiler();
        foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
            compiler.AddReference(File.ReadAllBytes(path));
        return compiler;
    }
    [Theory]
    [MemberData(nameof(Exercises))]
    public void Shared_exercise_starter_fails_and_reference_passes(string id, string json)
    {
        var item = JsonDocument.Parse(json).RootElement;
        var compiler = Compiler();
        var checks = item.GetProperty("checks").GetString()!;
        var starter = compiler.Compile(item.GetProperty("starter").GetString()!, checks);
        Assert.True(starter.Success, id + ": " + string.Join("; ", starter.Diagnostics));
        Assert.Contains(compiler.Execute().Checks, check => !check.Passed);
        var solution = compiler.Compile(item.GetProperty("solution").GetString()!, checks);
        Assert.True(solution.Success, id + ": " + string.Join("; ", solution.Diagnostics));
        var result = compiler.Execute();
        Assert.Null(result.Error);
        Assert.NotEmpty(result.Checks);
        Assert.All(result.Checks, check => Assert.True(check.Passed, id + ": " + check.Name));
    }
    [Fact]
    public void Compiler_reports_diagnostics_and_runtime_errors_and_bounds_output()
    {
        var compiler = Compiler();
        Assert.False(compiler.Compile("not C#", "").Success);
        Assert.NotNull(compiler.Execute().Error);
        Assert.False(compiler.Compile(new string('x', 102401), "").Success);
        Assert.True(compiler.Compile("", "public static class ExerciseChecks { public static string[] Run() => throw new System.InvalidOperationException(\"test failure\"); }").Success);
        Assert.Contains("test failure", compiler.Execute().Error);
        Assert.True(compiler.Compile("", "public static class ExerciseChecks { public static string[] Run() { System.Console.Write(new string('x', 100000)); return [\"+done\"]; } }").Success);
        var result = compiler.Execute();
        Assert.True(result.OutputTruncated);
        Assert.Equal(65536, result.Output.Length);
    }
}
