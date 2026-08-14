using System.Security.Cryptography;
using System.Text;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

public abstract class ArtifactProducerDecorator(IArtifactProducer inner) : IArtifactProducer
{
    protected IArtifactProducer Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

    public abstract RenderedArtifact Produce(Domain.ReportDocument document);
}

public sealed class WatermarkDecorator(IArtifactProducer inner, string watermark)
    : ArtifactProducerDecorator(inner)
{
    public override RenderedArtifact Produce(Domain.ReportDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(watermark);
        var artifact = Inner.Produce(document);
        var marker = artifact.Format == "html"
            ? $"\n<!-- WATERMARK: {watermark} -->"
            : $"\n[WATERMARK: {watermark}]";
        return artifact.AddDecoration(
            marker,
            "Watermark",
            watermark,
            $"WatermarkDecorator: {watermark}");
    }
}

public sealed class SignatureDecorator(IArtifactProducer inner) : ArtifactProducerDecorator(inner)
{
    public override RenderedArtifact Produce(Domain.ReportDocument document)
    {
        var artifact = Inner.Produce(document);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(artifact.Content));
        var signature = Convert.ToHexString(hash)[..16];
        var marker = artifact.Format == "html"
            ? $"\n<!-- SIGNATURE: {signature} -->"
            : $"\n[SIGNATURE: {signature}]";
        return artifact.AddDecoration(
            marker,
            "Signature",
            signature,
            $"SignatureDecorator: SHA256={signature}");
    }
}

public sealed class AuditDecorator(IArtifactProducer inner, string actor) : ArtifactProducerDecorator(inner)
{
    public override RenderedArtifact Produce(Domain.ReportDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var artifact = Inner.Produce(document);
        var record = $"actor={actor};report={document.ReportId}";
        var marker = artifact.Format == "html"
            ? $"\n<!-- AUDIT: {record} -->"
            : $"\n[AUDIT: {record}]";
        return artifact.AddDecoration(
            marker,
            "AuditActor",
            actor,
            $"AuditDecorator: {record}");
    }
}
