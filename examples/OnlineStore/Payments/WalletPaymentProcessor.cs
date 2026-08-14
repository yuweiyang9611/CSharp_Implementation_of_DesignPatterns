using DesignPatterns.TeachingProjects.OnlineStore.Application;

namespace DesignPatterns.TeachingProjects.OnlineStore.Payments;

public sealed class WalletPaymentProcessor(
    string walletId,
    decimal availableBalance,
    ICheckoutTrace trace) : IPaymentProcessor
{
    public PaymentResult Process(PaymentRequest request)
    {
        trace.Add($"[Payment] 钱包 {walletId} 请求扣款 {request.Amount:C2}。");
        if (availableBalance < request.Amount)
        {
            trace.Add("[Payment] 支付拒绝：钱包余额不足。");
            return new PaymentResult(false, string.Empty, "钱包余额不足。");
        }

        string reference = $"WALLET-{request.OrderNumber}";
        trace.Add($"[Payment] 支付成功，流水号 {reference}。");
        return new PaymentResult(true, reference, "支付成功。");
    }
}
