namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public static class ApplicationEntryPoint
{
    public static int Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                DemoScenario.Run(echoTrace: true);
                return 0;
            }

            if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return SelfTestRunner.Run(Console.Out);
            }

            Console.Error.WriteLine("用法: dotnet run [--self-test]");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"运行失败: {exception.Message}");
            return 1;
        }
    }
}
