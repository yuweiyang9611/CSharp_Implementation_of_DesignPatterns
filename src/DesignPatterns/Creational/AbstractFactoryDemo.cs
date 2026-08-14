namespace DesignPatterns.Creational;

/// <summary>
/// Demonstrates creation of compatible UI-control families without naming concrete controls in client code.
/// </summary>
public sealed class AbstractFactoryDemo : IPatternDemo
{
    public string Key => "abstract-factory";

    public string Name => "Abstract Factory / 抽象工厂模式";

    public string Category => "Creational";

    public string Intent => "创建一组相互兼容的产品，而无需让客户端依赖具体产品类型。";

    public IReadOnlyList<string> Run()
    {
        IWidgetFactory desktopFactory = new WindowsWidgetFactory();
        IWidgetFactory mobileFactory = new MobileWidgetFactory();

        return
        [
            "桌面端控件族:",
            .. RenderCheckout(desktopFactory),
            "移动端控件族:",
            .. RenderCheckout(mobileFactory)
        ];
    }

    // Abstract products describe every member of a compatible family.
    private interface IButton
    {
        string Render(string caption);
    }

    private interface ITextField
    {
        string Render(string label, string value);
    }

    // Abstract factory grows when a new product kind is added; this is the pattern's main tradeoff.
    private interface IWidgetFactory
    {
        string FamilyName { get; }

        IButton CreateButton();

        ITextField CreateTextField();
    }

    private sealed class WindowsWidgetFactory : IWidgetFactory
    {
        public string FamilyName => "Windows Fluent";

        public IButton CreateButton() => new FluentButton();

        public ITextField CreateTextField() => new FluentTextField();
    }

    private sealed class MobileWidgetFactory : IWidgetFactory
    {
        public string FamilyName => "Mobile Touch";

        public IButton CreateButton() => new TouchButton();

        public ITextField CreateTextField() => new TouchTextField();
    }

    private sealed class FluentButton : IButton
    {
        public string Render(string caption) => $"[FluentButton caption='{caption}' density='compact']";
    }

    private sealed class FluentTextField : ITextField
    {
        public string Render(string label, string value) => $"[FluentTextField {label}: {value}]";
    }

    private sealed class TouchButton : IButton
    {
        public string Render(string caption) => $"[TouchButton caption='{caption}' min-height='48']";
    }

    private sealed class TouchTextField : ITextField
    {
        public string Render(string label, string value) => $"[TouchTextField floating-label='{label}' value='{value}']";
    }

    // Client: it only knows the abstract family, so it cannot accidentally mix visual styles.
    private static IReadOnlyList<string> RenderCheckout(IWidgetFactory factory)
    {
        var address = factory.CreateTextField();
        var submit = factory.CreateButton();

        return
        [
            $"主题: {factory.FamilyName}",
            address.Render("收货地址", "Chiyoda, Tokyo"),
            submit.Render("提交订单")
        ];
    }
}
