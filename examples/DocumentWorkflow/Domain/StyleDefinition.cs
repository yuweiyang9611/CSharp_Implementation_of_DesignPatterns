namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

/// <summary>
/// 不可变的共享样式对象。不可变性让多个章节安全地持有同一个实例。
/// </summary>
public sealed record StyleDefinition(
    string Name,
    string FontFamily,
    int FontSize,
    string ColorHex);
