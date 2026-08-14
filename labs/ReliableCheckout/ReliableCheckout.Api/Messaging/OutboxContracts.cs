using ReliableCheckout.Domain;

namespace ReliableCheckout.Messaging;

public interface IOutboxHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken);
}

public sealed record DispatchReport(int Processed, int Failed);

public interface IOutboxDispatcher
{
    Task<DispatchReport> DispatchBatchAsync(CancellationToken cancellationToken = default);
}
