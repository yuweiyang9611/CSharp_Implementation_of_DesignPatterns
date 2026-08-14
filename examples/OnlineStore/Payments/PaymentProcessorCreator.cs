using DesignPatterns.TeachingProjects.OnlineStore.Application;

namespace DesignPatterns.TeachingProjects.OnlineStore.Payments;

public abstract class PaymentProcessorCreator(ICheckoutTrace trace)
{
    protected ICheckoutTrace Trace { get; } = trace;

    public PaymentResult Pay(PaymentRequest request)
    {
        IPaymentProcessor processor = CreateProcessor(request.Method);
        Trace.Add($"[Factory Method] {GetType().Name} 创建 {processor.GetType().Name}。");
        return processor.Process(request);
    }

    protected abstract IPaymentProcessor CreateProcessor(PaymentMethod method);
}

public sealed class WalletPaymentProcessorCreator(ICheckoutTrace trace) : PaymentProcessorCreator(trace)
{
    protected override IPaymentProcessor CreateProcessor(PaymentMethod method)
    {
        return new WalletPaymentProcessor(method.WalletId, method.AvailableBalance, Trace);
    }
}
