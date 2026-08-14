using DesignPatterns.TeachingProjects.OnlineStore.Domain;
using DesignPatterns.TeachingProjects.OnlineStore.Payments;
using DesignPatterns.TeachingProjects.OnlineStore.Validation;

namespace DesignPatterns.TeachingProjects.OnlineStore.Application;

public sealed record CheckoutResult(
    bool IsSuccess,
    string Message,
    ValidationReport Validation,
    Order? Order,
    PaymentResult? Payment);
