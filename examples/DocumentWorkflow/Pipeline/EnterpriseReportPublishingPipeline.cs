using DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Pipeline;

public sealed class EnterpriseReportPublishingPipeline : PublishingPipeline
{
    protected override IOutputComponentFactory CreateOutputComponentFactory(OutputChannel channel) =>
        channel switch
        {
            OutputChannel.Web => new WebOutputComponentFactory(),
            OutputChannel.Print => new PrintOutputComponentFactory(),
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "不支持的输出渠道。")
        };
}
