using System.Text;
using System.Text.Json;
using DesignPatterns;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return 0;
}
if (args.Contains("--list", StringComparer.OrdinalIgnoreCase))
{
    PrintCatalog(PatternCatalog.All);
    return 0;
}

if (args.Contains("--catalog-json", StringComparer.OrdinalIgnoreCase))
{
    PrintCatalogJson(PatternCatalog.All);
    return 0;
}

if (args.Contains("--evidence-json", StringComparer.OrdinalIgnoreCase))
{
    PrintEvidenceJson(PatternCatalog.All);
    return 0;
}

if (args.Contains("--all", StringComparer.OrdinalIgnoreCase))
{
    foreach (var demo in PatternCatalog.All)
    {
        RunDemo(demo);
    }

    return 0;
}

var categoryIndex = Array.FindIndex(args, argument => argument.Equals("--category", StringComparison.OrdinalIgnoreCase));
if (categoryIndex >= 0)
{
    if (categoryIndex == args.Length - 1)
    {
        Console.Error.WriteLine("--category 后需要 Creational、Structural 或 Behavioral。");
        return 2;
    }

    var category = args[categoryIndex + 1];
    var matches = PatternCatalog.All
        .Where(demo => demo.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if (matches.Length == 0)
    {
        Console.Error.WriteLine($"未知分类：{category}");
        return 2;
    }

    foreach (var demo in matches)
    {
        RunDemo(demo);
    }

    return 0;
}

var selected = PatternCatalog.Find(args[0]);
if (selected is null)
{
    Console.Error.WriteLine($"未知模式 Key：{args[0]}");
    Console.Error.WriteLine("执行 --list 查看可用 Key。");
    return 2;
}

RunDemo(selected);
return 0;

static void RunDemo(IPatternDemo demo)
{
    Console.WriteLine($"\n=== {demo.Name} [{demo.Category}] ===");
    Console.WriteLine($"意图：{demo.Intent}");
    foreach (var line in demo.Run())
    {
        Console.WriteLine($"  {line}");
    }
}

static void PrintCatalog(IEnumerable<IPatternDemo> demos)
{
    Console.WriteLine("序号  Key                       分类         名称");
    Console.WriteLine(new string('-', 76));
    var index = 1;
    foreach (var demo in demos)
    {
        Console.WriteLine($"{index,2}.   {demo.Key,-25} {demo.Category,-12} {demo.Name}");
        index++;
    }
}

static void PrintCatalogJson(IEnumerable<IPatternDemo> demos)
{
    var catalog = demos.Select((demo, index) => new
    {
        number = index + 1,
        key = demo.Key,
        name = demo.Name,
        category = demo.Category,
        intent = demo.Intent,
    });
    Console.WriteLine(JsonSerializer.Serialize(catalog, new JsonSerializerOptions
    {
        WriteIndented = true,
    }));
}

static void PrintEvidenceJson(IEnumerable<IPatternDemo> demos)
{
    var evidence = demos.Select((demo, index) => new
    {
        number = index + 1,
        key = demo.Key,
        name = demo.Name,
        category = demo.Category,
        intent = demo.Intent,
        output = demo.Run().ToArray(),
    });
    Console.WriteLine(JsonSerializer.Serialize(evidence, new JsonSerializerOptions
    {
        WriteIndented = true,
    }));
}

static void PrintUsage()
{
    Console.WriteLine("C# Design Patterns Runner");
    Console.WriteLine();
    Console.WriteLine("用法：");
    Console.WriteLine("  dotnet run --project src/DesignPatterns.Runner -- --list");
    Console.WriteLine("  dotnet run --project src/DesignPatterns.Runner -- --catalog-json");
    Console.WriteLine("  dotnet run --project src/DesignPatterns.Runner -- --evidence-json");
    Console.WriteLine("  dotnet run --project src/DesignPatterns.Runner -- iterator");
    Console.WriteLine("  dotnet run --project src/DesignPatterns.Runner -- --category Behavioral");
    Console.WriteLine("  dotnet run --project src/DesignPatterns.Runner -- --all");
}
