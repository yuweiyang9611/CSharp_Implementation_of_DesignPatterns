using System.Collections.ObjectModel;
using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using DesignPatterns.Structural;

namespace DesignPatterns;

/// <summary>
/// Keeps the demos in the same beginner-friendly order as the source learning path.
/// </summary>
public static class PatternCatalog
{
    private static readonly ReadOnlyCollection<IPatternDemo> Demos = Array.AsReadOnly<IPatternDemo>(
    [
        new IteratorDemo(),
        new AdapterDemo(),
        new TemplateMethodDemo(),
        new FactoryMethodDemo(),
        new SingletonDemo(),
        new PrototypeDemo(),
        new BuilderDemo(),
        new AbstractFactoryDemo(),
        new BridgeDemo(),
        new StrategyDemo(),
        new CompositeDemo(),
        new DecoratorDemo(),
        new VisitorDemo(),
        new ChainOfResponsibilityDemo(),
        new FacadeDemo(),
        new MediatorDemo(),
        new ObserverDemo(),
        new MementoDemo(),
        new StateDemo(),
        new FlyweightDemo(),
        new ProxyDemo(),
        new CommandDemo(),
        new InterpreterDemo(),
    ]);

    public static IReadOnlyList<IPatternDemo> All => Demos;

    public static IPatternDemo? Find(string key) =>
        Demos.FirstOrDefault(demo => demo.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
