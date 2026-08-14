namespace DesignPatterns.Creational;

/// <summary>
/// Demonstrates a thread-safe, lazy singleton with immutable application settings.
/// </summary>
public sealed class SingletonDemo : IPatternDemo
{
    public string Key => "singleton";

    public string Name => "Singleton / 单例模式";

    public string Category => "Creational";

    public string Intent => "保证一个类型只有一个实例，并提供统一的全局访问入口。";

    public IReadOnlyList<string> Run()
    {
        var startupReader = ApplicationSettings.Instance;
        var requestReader = ApplicationSettings.Instance;

        return
        [
            $"启动阶段读取环境: {startupReader.Environment}",
            $"请求阶段读取 API: {requestReader.ApiBaseUrl}",
            $"超时设置: {requestReader.TimeoutSeconds} 秒",
            $"两个调用者持有同一实例: {ReferenceEquals(startupReader, requestReader)}"
        ];
    }

    // Singleton: private constructor prevents callers from creating competing instances.
    // Lazy<T> also supplies safe publication when multiple threads access Instance together.
    private sealed class ApplicationSettings
    {
        private static readonly Lazy<ApplicationSettings> LazyInstance = new(
            () => new ApplicationSettings(
                environment: "Production",
                apiBaseUrl: "https://api.example.test",
                timeoutSeconds: 30),
            LazyThreadSafetyMode.ExecutionAndPublication);

        private ApplicationSettings(string environment, string apiBaseUrl, int timeoutSeconds)
        {
            Environment = environment;
            ApiBaseUrl = apiBaseUrl;
            TimeoutSeconds = timeoutSeconds;
        }

        public static ApplicationSettings Instance => LazyInstance.Value;

        public string Environment { get; }

        public string ApiBaseUrl { get; }

        public int TimeoutSeconds { get; }
    }
}
