using System.Diagnostics;
using ReliableCheckout.Domain;

namespace ReliableCheckout.Tests;

public sealed class ProcessRecoveryTests
{
    [Theory]
    [InlineData("payment:after-provider", 0L)]
    [InlineData("outbox:after-handler:PaymentRequested", 1L)]
    public async Task Killed_worker_recovers_without_creating_a_second_payment(string crashPoint, long receiptsBefore)
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        var order = await ReliabilityTests.CreateAsync(factory);
        var marker = factory.DatabasePath + ".marker";
        try
        {
            using (var first = Start(factory.DatabasePath, crashPoint, marker))
            {
                try
                {
                    await UntilAsync(() => Task.FromResult(File.Exists(marker)), first);
                    Assert.Equal(1, factory.LegacyPaymentSdk.RequestCount);
                    Assert.Equal(receiptsBefore, await ReliabilityTests.ScalarAsync(factory,
                        "SELECT COUNT(*) FROM consumer_receipts WHERE consumer = 'payment-requested';"));
                }
                finally { if (!first.HasExited) { first.Kill(entireProcessTree: true); await first.WaitForExitAsync(); } }
            }
            using var recovery = Start(factory.DatabasePath, "none", marker);
            try
            {
                await UntilAsync(async () => (await factory.Store.GetOutboxAsync()).All(item => item.ProcessedAt is not null), recovery);
                Assert.Equal(1, factory.LegacyPaymentSdk.RequestCount);
                Assert.Equal(PaymentStatus.Requested, (await factory.Store.GetOrderAsync(order.Id))!.PaymentStatus);
                Assert.Equal(OrderStatus.AwaitingPayment, (await factory.Store.GetOrderAsync(order.Id))!.Status);
                Assert.Equal("Held", (await factory.Store.GetOrderAsync(order.Id))!.ReservationState);
                Assert.Equal(3, await factory.Store.GetInventoryAsync("TEST"));
                Assert.Equal(1L, await ReliabilityTests.ScalarAsync(factory,
                    "SELECT COUNT(*) FROM consumer_receipts WHERE consumer = 'payment-requested';"));
            }
            finally { if (!recovery.HasExited) { recovery.Kill(true); await recovery.WaitForExitAsync(); } }
        }
        finally { if (File.Exists(marker)) File.Delete(marker); }
    }

    [Fact]
    public async Task Two_worker_processes_share_the_queue_without_duplicate_provider_creation()
    {
        using var factory = new ReliableCheckoutApplicationFactory();
        using var client = factory.CreateClient();
        var firstMarker = factory.DatabasePath + ".first-ready";
        var secondMarker = factory.DatabasePath + ".second-ready";
        using var first = Start(factory.DatabasePath, "none", firstMarker);
        using var second = Start(factory.DatabasePath, "none", secondMarker);
        try
        {
            await UntilAsync(() => Task.FromResult(File.Exists(firstMarker)), first);
            await UntilAsync(() => Task.FromResult(File.Exists(secondMarker)), second);
            // Both independent hosts are listening before the HTTP API creates work.
            await factory.Store.SetInventoryAsync("TEST", 5);
            using var api = new HttpClient { BaseAddress = new Uri(await File.ReadAllTextAsync(firstMarker)) };
            api.DefaultRequestHeaders.Add("Idempotency-Key", "two-process-order");
            var response = await api.PostAsJsonAsync("/orders", new CreateOrderRequest("TEST", 2));
            response.EnsureSuccessStatusCode();
            await UntilAsync(async () => (await factory.Store.GetOutboxAsync()).All(item => item.ProcessedAt is not null), first);
            Assert.False(second.HasExited);
            Assert.Equal(1, factory.LegacyPaymentSdk.RequestCount);
            Assert.Equal(1L, await ReliabilityTests.ScalarAsync(factory,
                "SELECT COUNT(*) FROM consumer_receipts WHERE consumer = 'payment-requested';"));
        }
        finally
        {
            foreach (var process in new[] { first, second })
                if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
            foreach (var marker in new[] { firstMarker, secondMarker })
                if (File.Exists(marker)) File.Delete(marker);
        }
    }

    private static Process Start(string database, string point, string marker)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DesignPatterns.sln"))) root = root.Parent;
        Assert.NotNull(root);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var host = Path.Combine(root.FullName, "labs", "ReliableCheckout", "ReliableCheckout.ProcessHost", "bin", configuration, "net10.0", "ReliableCheckout.ProcessHost.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { host, database, point, marker }) start.ArgumentList.Add(argument);
        var process = Process.Start(start)!;
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }
    private static async Task UntilAsync(Func<Task<bool>> predicate, Process process)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await predicate())
        {
            Assert.False(process.HasExited, "Worker exited before recovery completed.");
            await Task.Delay(50, timeout.Token);
        }
    }
}
