namespace DesignPatterns.Creational;

/// <summary>
/// Demonstrates cloning a configured campaign template, including independent mutable collections.
/// </summary>
public sealed class PrototypeDemo : IPatternDemo
{
    public string Key => "prototype";

    public string Name => "Prototype / 原型模式";

    public string Category => "Creational";

    public string Intent => "通过复制现有原型创建对象，复用昂贵或复杂的初始配置。";

    public IReadOnlyList<string> Run()
    {
        var launchTemplate = new CampaignDocument(
            title: "Product launch",
            body: "Meet our new product.",
            channels: ["Email", "Web"]);

        var localizedCampaign = launchTemplate.Clone();
        localizedCampaign.Title = "新产品发布";
        localizedCampaign.Body = "欢迎了解我们的新产品。";
        localizedCampaign.Channels.Add("Mobile push");

        return
        [
            $"原型: {launchTemplate.Title} | {launchTemplate.Body}",
            $"原型渠道: {string.Join(", ", launchTemplate.Channels)}",
            $"克隆版: {localizedCampaign.Title} | {localizedCampaign.Body}",
            $"克隆版渠道: {string.Join(", ", localizedCampaign.Channels)}",
            $"渠道集合已深复制: {!ReferenceEquals(launchTemplate.Channels, localizedCampaign.Channels)}"
        ];
    }

    private interface IPrototype<out T>
    {
        T Clone();
    }

    private sealed class CampaignDocument : IPrototype<CampaignDocument>
    {
        public CampaignDocument(string title, string body, IEnumerable<string> channels)
        {
            Title = title;
            Body = body;
            Channels = [.. channels];
        }

        public string Title { get; set; }

        public string Body { get; set; }

        public List<string> Channels { get; }

        // A shallow MemberwiseClone would share Channels and let a clone corrupt its prototype.
        public CampaignDocument Clone() => new(Title, Body, Channels);
    }
}
