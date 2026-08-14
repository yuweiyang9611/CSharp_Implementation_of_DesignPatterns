namespace DesignPatterns.Behavioral;

/// <summary>
/// Represents a small authorization language as an expression tree.
/// </summary>
public sealed class InterpreterDemo : IPatternDemo
{
    public string Key => "interpreter";

    public string Name => "Interpreter / 解释器模式";

    public string Category => "Behavioral";

    public string Intent => "为简单语言建立语法表示，并解释该语言中的表达式。";

    public IReadOnlyList<string> Run()
    {
        // Grammar: (role = admin OR department = security) AND active = true.
        IRuleExpression policy = new AndExpression(
            new OrExpression(
                new ClaimEqualsExpression("role", "admin"),
                new ClaimEqualsExpression("department", "security")),
            new ClaimEqualsExpression("active", "true"));

        var users = new[]
        {
            new UserContext("Aiko", new Dictionary<string, string>
            {
                ["role"] = "admin",
                ["department"] = "finance",
                ["active"] = "true"
            }),
            new UserContext("Ben", new Dictionary<string, string>
            {
                ["role"] = "analyst",
                ["department"] = "security",
                ["active"] = "false"
            }),
            new UserContext("Chen", new Dictionary<string, string>
            {
                ["role"] = "analyst",
                ["department"] = "security",
                ["active"] = "true"
            })
        };

        var output = new List<string>
        {
            "Policy: (role = admin OR department = security) AND active = true"
        };

        foreach (var user in users)
        {
            output.Add($"{user.Name}: {(policy.Interpret(user) ? "allowed" : "denied")}");
        }

        return output;
    }

    private sealed record UserContext(string Name, IReadOnlyDictionary<string, string> Claims);

    private interface IRuleExpression
    {
        bool Interpret(UserContext context);
    }

    // Terminal expression: evaluates one primitive statement in the grammar.
    private sealed class ClaimEqualsExpression : IRuleExpression
    {
        private readonly string _claim;
        private readonly string _expectedValue;

        internal ClaimEqualsExpression(string claim, string expectedValue)
        {
            _claim = claim;
            _expectedValue = expectedValue;
        }

        public bool Interpret(UserContext context) =>
            context.Claims.TryGetValue(_claim, out var actualValue) &&
            string.Equals(actualValue, _expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    // Nonterminal expressions compose smaller expressions into a richer language.
    private sealed class AndExpression : IRuleExpression
    {
        private readonly IRuleExpression _left;
        private readonly IRuleExpression _right;

        internal AndExpression(IRuleExpression left, IRuleExpression right)
        {
            _left = left;
            _right = right;
        }

        public bool Interpret(UserContext context) =>
            _left.Interpret(context) && _right.Interpret(context);
    }

    private sealed class OrExpression : IRuleExpression
    {
        private readonly IRuleExpression _left;
        private readonly IRuleExpression _right;

        internal OrExpression(IRuleExpression left, IRuleExpression right)
        {
            _left = left;
            _right = right;
        }

        public bool Interpret(UserContext context) =>
            _left.Interpret(context) || _right.Interpret(context);
    }
}
