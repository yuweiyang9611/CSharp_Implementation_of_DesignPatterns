using DesignPatterns.TeachingProjects.OnlineStore.Application;

namespace DesignPatterns.TeachingProjects.OnlineStore.Validation;

public abstract class CheckoutValidationRule(ICheckoutTrace trace)
{
    private CheckoutValidationRule? _next;

    public CheckoutValidationRule SetNext(CheckoutValidationRule next)
    {
        _next = next;
        return next;
    }

    internal void Execute(
        CheckoutValidationContext context,
        ICollection<ValidationStep> steps)
    {
        ValidationStep step = Evaluate(context);
        steps.Add(step);
        trace.Add($"[Chain] {step.Rule}: {(step.Passed ? "通过" : "拒绝")} — {step.Message}");

        if (step.Passed)
        {
            _next?.Execute(context, steps);
        }
    }

    protected abstract ValidationStep Evaluate(CheckoutValidationContext context);
}
