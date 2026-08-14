namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

/// <summary>
/// 显式表达原型契约：调用方得到一个可独立修改的副本。
/// </summary>
/// <typeparam name="T">原型产生的对象类型。</typeparam>
public interface IPrototype<out T>
{
    T DeepClone();
}
