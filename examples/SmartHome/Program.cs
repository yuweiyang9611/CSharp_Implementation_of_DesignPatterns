using System.Text;
using DesignPatterns.TeachingProjects.SmartHome.Demo;
using DesignPatterns.TeachingProjects.SmartHome.Testing;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0)
{
    SmartHomeDemo.Run(Console.Out);
    return 0;
}

if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
{
    return SelfTestRunner.Run(Console.Out);
}

if (args.Length == 1 && (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("智能家居设计模式教学项目");
    Console.WriteLine("  默认运行：dotnet run --project examples/SmartHome");
    Console.WriteLine("  自检：    dotnet run --project examples/SmartHome -- --self-test");
    return 0;
}

Console.Error.WriteLine($"未知参数：{string.Join(' ', args)}");
Console.Error.WriteLine("使用 --help 查看可用命令。");
return 2;
