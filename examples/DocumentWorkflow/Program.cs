using System.Text;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Demo;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Testing;

Console.OutputEncoding = Encoding.UTF8;

try
{
    if (args.Length == 0)
    {
        var scenario = DocumentWorkflowScenario.Execute();
        Console.WriteLine(ScenarioReportFormatter.Format(scenario));
        return 0;
    }

    if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
    {
        return SelfTestRunner.Run(Console.Out, Console.Error);
    }

    Console.Error.WriteLine("用法：dotnet run --project DesignPatterns.TeachingProjects.DocumentWorkflow.csproj [--self-test]");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"文档发布场景失败：{exception.GetType().Name}: {exception.Message}");
    return 1;
}
