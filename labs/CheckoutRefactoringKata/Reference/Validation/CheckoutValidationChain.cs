using CheckoutRefactoringKata.Contracts;

namespace CheckoutRefactoringKata.Reference.Validation;

public sealed class CheckoutValidationChain(CheckoutValidationHandler firstHandler)
{
    public CheckoutFailure? Validate(CheckoutRequest request) =>
        firstHandler.Validate(request);

    public static CheckoutValidationChain CreateDefault()
    {
        var emptyCart = new EmptyCartHandler();
        emptyCart
            .SetNext(new TermsAcceptedHandler())
            .SetNext(new PositiveQuantityHandler())
            .SetNext(new StockHandler())
            .SetNext(new PaymentTokenHandler());

        return new CheckoutValidationChain(emptyCart);
    }
}

public abstract class CheckoutValidationHandler
{
    private CheckoutValidationHandler? _next;

    public CheckoutValidationHandler SetNext(CheckoutValidationHandler next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        return next;
    }

    public CheckoutFailure? Validate(CheckoutRequest request) =>
        Check(request) ?? _next?.Validate(request);

    protected abstract CheckoutFailure? Check(CheckoutRequest request);
}

public sealed class EmptyCartHandler : CheckoutValidationHandler
{
    protected override CheckoutFailure? Check(CheckoutRequest request) =>
        request.Items.Count == 0
            ? new(CheckoutErrorCode.EmptyCart, "购物车不能为空。")
            : null;
}

public sealed class TermsAcceptedHandler : CheckoutValidationHandler
{
    protected override CheckoutFailure? Check(CheckoutRequest request) =>
        !request.TermsAccepted
            ? new(CheckoutErrorCode.TermsNotAccepted, "必须接受结账条款。")
            : null;
}

public sealed class PositiveQuantityHandler : CheckoutValidationHandler
{
    protected override CheckoutFailure? Check(CheckoutRequest request) =>
        request.Items.Any(item => item.Quantity <= 0)
            ? new(CheckoutErrorCode.InvalidQuantity, "商品数量必须大于 0。")
            : null;
}

public sealed class StockHandler : CheckoutValidationHandler
{
    protected override CheckoutFailure? Check(CheckoutRequest request) =>
        request.Items.Any(item => item.Quantity > item.AvailableStock)
            ? new(CheckoutErrorCode.OutOfStock, "至少一件商品库存不足。")
            : null;
}

public sealed class PaymentTokenHandler : CheckoutValidationHandler
{
    protected override CheckoutFailure? Check(CheckoutRequest request) =>
        string.IsNullOrWhiteSpace(request.PaymentToken)
            ? new(CheckoutErrorCode.PaymentTokenMissing, "缺少支付令牌。")
            : null;
}
