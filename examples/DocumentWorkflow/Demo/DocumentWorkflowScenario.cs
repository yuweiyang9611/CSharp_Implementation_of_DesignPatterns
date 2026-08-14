using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Output;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Pipeline;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Demo;

public sealed record DocumentWorkflowScenarioResult(
    ReportDocument Template,
    IReadOnlyList<StyleDefinition> SharedStyles,
    bool RepeatedBodyStyleIsShared,
    PublishingResult WebPublication,
    PublishingResult PrintPublication);

public static class DocumentWorkflowScenario
{
    public const string FilterSource = "audience = external AND NOT (tag = draft)";

    public static DocumentWorkflowScenarioResult Execute()
    {
        var styleFactory = new StyleFlyweightFactory();
        var calloutStyle = styleFactory.GetOrCreate("Callout", "Noto Sans", 16, "#17365D");
        var tableStyle = styleFactory.GetOrCreate("FinancialTable", "Noto Sans Mono", 10, "#222222");
        var bodyStyle = styleFactory.GetOrCreate("Body", "Noto Serif", 11, "#333333");
        var repeatedBodyStyle = styleFactory.GetOrCreate("Body", "Noto Serif", 11, "#333333");

        var sections = new SectionCollection();
        sections.Add(new ReportSection(
            "SEC-01",
            "Executive Summary",
            "Revenue grew steadily while service quality and customer retention improved.",
            Audience.External,
            1,
            calloutStyle,
            ["public", "summary"]));
        sections.Add(new ReportSection(
            "SEC-02",
            "Revenue Details",
            "Regional revenue tables contain internal forecasts and margin assumptions.",
            Audience.Internal,
            3,
            tableStyle,
            ["internal", "finance"]));
        sections.Add(new ReportSection(
            "SEC-03",
            "Customer Outcomes",
            "Customers adopted the new portal and completed onboarding with fewer support requests.",
            Audience.External,
            2,
            bodyStyle,
            ["public", "customers"]));
        sections.Add(new ReportSection(
            "SEC-04",
            "Draft Notes",
            "Unreviewed notes remain visible only while the report is being prepared.",
            Audience.External,
            1,
            repeatedBodyStyle,
            ["draft"]));

        var template = new ReportDocument(
            "TEMPLATE-QUARTERLY",
            "Quarterly Report Template",
            "Corporate Reporting",
            sections,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Classification"] = "Internal Template",
                ["Owner"] = "Reporting Office",
                ["Period"] = "TEMPLATE"
            });

        var commonMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Classification"] = "External",
            ["Owner"] = "Reporting Office",
            ["Period"] = "2026-Q2"
        };
        var pipeline = new EnterpriseReportPublishingPipeline();

        var web = pipeline.Publish(new PublishingRequest(
            template,
            "QTR-2026-Q2",
            "2026 Q2 Service Performance Report",
            commonMetadata,
            FilterSource,
            OutputChannel.Web,
            "TRAINING COPY",
            "reporting.bot"));

        var print = pipeline.Publish(new PublishingRequest(
            template,
            "QTR-2026-Q2",
            "2026 Q2 Service Performance Report",
            commonMetadata,
            FilterSource,
            OutputChannel.Print,
            "TRAINING COPY",
            "reporting.bot"));

        return new DocumentWorkflowScenarioResult(
            template,
            styleFactory.Snapshot(),
            ReferenceEquals(bodyStyle, repeatedBodyStyle),
            web,
            print);
    }
}
