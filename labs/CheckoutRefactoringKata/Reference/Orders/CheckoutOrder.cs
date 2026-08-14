using CheckoutRefactoringKata.Contracts;

namespace CheckoutRefactoringKata.Reference.Orders;

/// <summary>
/// Context 只暴露业务动作；每个 State 决定动作在当前状态是否合法以及下一个状态。
/// </summary>
public sealed class CheckoutOrder
{
    private CheckoutOrderState _state = DraftOrderState.Instance;
    private readonly IList<string> _trace;

    public CheckoutOrder(string orderId, IList<string> trace)
    {
        // 订单号是否为空不属于本工坊既有校验规则；这里不额外收紧 Starter 的契约。
        OrderId = orderId;
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _trace.Add("order:draft");
    }

    public string OrderId { get; }

    public CheckoutStatus Status => _state.Status;

    public void MarkValidated() => _state.MarkValidated(this);

    public void MarkPaid() => _state.MarkPaid(this);

    public void Complete() => _state.Complete(this);

    internal void TransitionTo(CheckoutOrderState nextState)
    {
        _state = nextState;
        _trace.Add($"order:{nextState.Status.ToString().ToLowerInvariant()}");
    }
}

internal abstract class CheckoutOrderState
{
    public abstract CheckoutStatus Status { get; }

    public virtual void MarkValidated(CheckoutOrder order) => ThrowInvalid("校验");

    public virtual void MarkPaid(CheckoutOrder order) => ThrowInvalid("支付");

    public virtual void Complete(CheckoutOrder order) => ThrowInvalid("完成");

    private void ThrowInvalid(string action) =>
        throw new InvalidOperationException(
            $"订单处于 {Status} 状态时不能执行“{action}”。");
}

internal sealed class DraftOrderState : CheckoutOrderState
{
    public static DraftOrderState Instance { get; } = new();

    public override CheckoutStatus Status => CheckoutStatus.Draft;

    public override void MarkValidated(CheckoutOrder order) =>
        order.TransitionTo(ValidatedOrderState.Instance);
}

internal sealed class ValidatedOrderState : CheckoutOrderState
{
    public static ValidatedOrderState Instance { get; } = new();

    public override CheckoutStatus Status => CheckoutStatus.Validated;

    public override void MarkPaid(CheckoutOrder order) =>
        order.TransitionTo(PaidOrderState.Instance);
}

internal sealed class PaidOrderState : CheckoutOrderState
{
    public static PaidOrderState Instance { get; } = new();

    public override CheckoutStatus Status => CheckoutStatus.Paid;

    public override void Complete(CheckoutOrder order) =>
        order.TransitionTo(CompletedOrderState.Instance);
}

internal sealed class CompletedOrderState : CheckoutOrderState
{
    public static CompletedOrderState Instance { get; } = new();

    public override CheckoutStatus Status => CheckoutStatus.Completed;
}
