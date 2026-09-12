using Microsoft.Extensions.DependencyInjection.Extensions;
using ReliableCheckout.Infrastructure;

// Only this test executable can pause at a crash boundary. The application has no crash endpoint/configuration.
var databasePath = args[0];
var crashPoint = args[1];
var marker = args[2];
var app = await CheckoutApplication.CreateAsync(["--urls", "http://127.0.0.1:0"], builder =>
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Checkout"] = $"Data Source={databasePath};Pooling=False;Default Timeout=10",
        ["ReliableCheckout:SeedDemoInventory"] = "false",
        ["ReliableCheckout:LeaseSeconds"] = "1",
        ["ReliableCheckout:LeaseRenewalSeconds"] = "0.2",
        ["ReliableCheckout:OutboxPollingMilliseconds"] = "50"
    });
    builder.Services.RemoveAll<IFailureInjector>();
    builder.Services.AddSingleton<IFailureInjector>(new ProcessPause(crashPoint, marker));
});
await app.StartAsync();
Console.WriteLine("READY " + app.Urls.Single());
if (crashPoint == "none") File.WriteAllText(marker, app.Urls.Single());
await app.WaitForShutdownAsync();

sealed class ProcessPause(string point, string marker) : IFailureInjector
{
    public void FailNext(string name, int count = 1) => throw new NotSupportedException();
    public void ThrowIfScheduled(string name)
    {
        if (name != point) return;
        File.WriteAllText(marker, name);
        // The parent kills this real process after observing the durable boundary marker.
        Thread.Sleep(Timeout.Infinite);
    }
}
