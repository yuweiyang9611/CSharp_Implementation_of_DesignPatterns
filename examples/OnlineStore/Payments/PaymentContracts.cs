namespace DesignPatterns.TeachingProjects.OnlineStore.Payments;

public sealed record PaymentMethod(string WalletId, decimal AvailableBalance);

public sealed record PaymentRequest(
    string OrderNumber,
    decimal Amount,
    PaymentMethod Method);

public sealed record PaymentResult(
    bool IsApproved,
    string Reference,
    string Message);

public interface IPaymentProcessor
{
    PaymentResult Process(PaymentRequest request);
}
