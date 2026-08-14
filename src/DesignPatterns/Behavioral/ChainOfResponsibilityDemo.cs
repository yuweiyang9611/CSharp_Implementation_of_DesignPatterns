using System.Globalization;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Routes expense requests through approvers without coupling the caller to a specific approver.
/// </summary>
public sealed class ChainOfResponsibilityDemo : IPatternDemo
{
    public string Key => "chain-of-responsibility";

    public string Name => "Chain of Responsibility / 职责链模式";

    public string Category => "Behavioral";

    public string Intent => "将请求沿处理者链传递，直到某个处理者能够处理它。";

    public IReadOnlyList<string> Run()
    {
        var output = new List<string>();
        var teamLead = new Approver("Team lead", 1_000m, output);
        var departmentManager = new Approver("Department manager", 5_000m, output);
        var financeDirector = new Approver("Finance director", 25_000m, output);

        teamLead.SetNext(departmentManager).SetNext(financeDirector);

        teamLead.Handle(new ExpenseRequest("Office supplies", 750m));
        teamLead.Handle(new ExpenseRequest("Training workshop", 3_200m));
        teamLead.Handle(new ExpenseRequest("Build server", 18_500m));
        teamLead.Handle(new ExpenseRequest("New office lease", 40_000m));

        return output;
    }

    private sealed record ExpenseRequest(string Description, decimal Amount);

    // Handler: it either processes the request or forwards it to its successor.
    private sealed class Approver
    {
        private readonly string _role;
        private readonly decimal _approvalLimit;
        private readonly ICollection<string> _output;
        private Approver? _next;

        internal Approver(string role, decimal approvalLimit, ICollection<string> output)
        {
            _role = role;
            _approvalLimit = approvalLimit;
            _output = output;
        }

        internal Approver SetNext(Approver next)
        {
            _next = next;
            return next;
        }

        internal void Handle(ExpenseRequest request)
        {
            var amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture);
            if (request.Amount <= _approvalLimit)
            {
                _output.Add($"{_role} approved {request.Description} for {amount}.");
                return;
            }

            if (_next is not null)
            {
                _output.Add($"{_role} forwarded {request.Description} ({amount}).");
                _next.Handle(request);
                return;
            }

            _output.Add($"{_role} rejected {request.Description}; {amount} exceeds the chain limit.");
        }
    }
}
