using System.Collections.ObjectModel;

namespace DesignPatterns.Creational;

/// <summary>
/// Demonstrates construction of validated deployment plans with a fluent builder and reusable recipes.
/// </summary>
public sealed class BuilderDemo : IPatternDemo
{
    public string Key => "builder";

    public string Name => "Builder / 建造者模式";

    public string Category => "Creational";

    public string Intent => "把复杂对象的分步构建与最终表示分离，使同一过程能创建不同配置。";

    public IReadOnlyList<string> Run()
    {
        var previewPlan = DeploymentPlanDirector.CreatePreview(new DeploymentPlanBuilder());
        var productionPlan = DeploymentPlanDirector.CreateProduction(new DeploymentPlanBuilder());

        return
        [
            "预览发布计划:",
            .. previewPlan.Describe(),
            "生产发布计划:",
            .. productionPlan.Describe()
        ];
    }

    // Product is immutable; the builder takes responsibility for validation and defensive copying.
    private sealed record DeploymentPlan(
        string Environment,
        ReadOnlyCollection<string> Steps,
        bool RequiresApproval)
    {
        public IReadOnlyList<string> Describe() =>
        [
            $"环境: {Environment}",
            $"步骤: {string.Join(" -> ", Steps)}",
            $"需要人工审批: {RequiresApproval}"
        ];
    }

    private sealed class DeploymentPlanBuilder
    {
        private string? _environment;
        private readonly List<string> _steps = [];
        private bool _requiresApproval;

        public DeploymentPlanBuilder ForEnvironment(string environment)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(environment);
            _environment = environment;
            return this;
        }

        public DeploymentPlanBuilder AddStep(string step)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(step);
            _steps.Add(step);
            return this;
        }

        public DeploymentPlanBuilder RequireApproval()
        {
            _requiresApproval = true;
            return this;
        }

        public DeploymentPlan Build()
        {
            if (_environment is null)
            {
                throw new InvalidOperationException("A deployment environment is required.");
            }

            if (_steps.Count == 0)
            {
                throw new InvalidOperationException("At least one deployment step is required.");
            }

            return new DeploymentPlan(
                _environment,
                Array.AsReadOnly(_steps.ToArray()),
                _requiresApproval);
        }
    }

    // Director captures common construction recipes; callers may also use the builder directly.
    private static class DeploymentPlanDirector
    {
        public static DeploymentPlan CreatePreview(DeploymentPlanBuilder builder) => builder
            .ForEnvironment("Preview")
            .AddStep("Build")
            .AddStep("Unit tests")
            .AddStep("Deploy ephemeral slot")
            .Build();

        public static DeploymentPlan CreateProduction(DeploymentPlanBuilder builder) => builder
            .ForEnvironment("Production")
            .AddStep("Build")
            .AddStep("Full test suite")
            .AddStep("Database backup")
            .AddStep("Blue-green deploy")
            .RequireApproval()
            .Build();
    }
}
