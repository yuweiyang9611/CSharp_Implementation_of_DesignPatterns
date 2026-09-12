using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ReliableCheckout.Tests;

internal sealed class ReliableCheckoutApplicationFactory : WebApplicationFactory<Program>
{
    public Action<IServiceCollection>? ConfigureServices { get; init; }
    public Dictionary<string, string?> Settings { get; } = [];
    public string DatabasePath => databasePath;
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"reliable-checkout-{Guid.NewGuid():N}.db");

    public ManualClock Clock { get; } = new(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

    public CheckoutStore Store => Services.GetRequiredService<CheckoutStore>();

    public IOutboxDispatcher Dispatcher => Services.GetRequiredService<IOutboxDispatcher>();

    public DeterministicFailureInjector Failures => Services.GetRequiredService<DeterministicFailureInjector>();

    public PersistentPaymentSdk LegacyPaymentSdk => Services.GetRequiredService<PersistentPaymentSdk>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Checkout"] =
                    $"Data Source={databasePath};Foreign Keys=True;Default Timeout=10;Pooling=False",
                ["ReliableCheckout:SeedDemoInventory"] = "false"
            });
            configuration.AddInMemoryCollection(Settings);
        });
        builder.ConfigureServices(services =>
        {
            // Dispatch is driven explicitly by the tests so time and failure order are deterministic.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
            ConfigureServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(databasePath + ".provider.db")) File.Delete(databasePath + ".provider.db");
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        var walPath = $"{databasePath}-wal";
        if (File.Exists(walPath))
        {
            File.Delete(walPath);
        }

        var sharedMemoryPath = $"{databasePath}-shm";
        if (File.Exists(sharedMemoryPath))
        {
            File.Delete(sharedMemoryPath);
        }
    }
}
