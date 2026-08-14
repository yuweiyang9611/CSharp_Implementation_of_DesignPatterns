using System.Security.Cryptography;
using System.Text;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Filtering;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

namespace DesignPatterns.TeachingProjects.Tests;

public sealed class DocumentWorkflowTests
{
    [Fact]
    public void Prototype_DeepCopiesMutableContentWhileSharingImmutableStyle()
    {
        var styles = new StyleFlyweightFactory();
        StyleDefinition style = styles.GetOrCreate("Body", "Noto Serif", 11, "#333333");
        ReportDocument template = CreateDocument(CreateSection("S-1", Audience.External, style, "public"));

        ReportDocument clone = template.DeepClone();
        clone.SetPublicationIdentity("REPORT-1", "Published Report");
        clone.Sections[0].Rename("Published Section");
        clone.Sections[0].AddTag("published");
        clone.SetMetadata("Status", "Published");

        Assert.NotSame(template, clone);
        Assert.NotSame(template.Sections[0], clone.Sections[0]);
        Assert.Same(template.Sections[0].Style, clone.Sections[0].Style);
        Assert.Equal("Template", template.Title);
        Assert.Equal("Original Section", template.Sections[0].Title);
        Assert.DoesNotContain("published", template.Sections[0].Tags, StringComparer.OrdinalIgnoreCase);
        Assert.False(template.Metadata.ContainsKey("Status"));
    }

    [Fact]
    public void Flyweight_ReusesEquivalentStyleAndRejectsConflictingDefinition()
    {
        var factory = new StyleFlyweightFactory();

        StyleDefinition first = factory.GetOrCreate("Body", "Noto Serif", 11, "#333333");
        StyleDefinition repeated = factory.GetOrCreate("body", "Noto Serif", 11, "#333333");

        Assert.Same(first, repeated);
        Assert.Equal(1, factory.SharedStyleCount);
        Assert.Throws<InvalidOperationException>(() =>
            factory.GetOrCreate("Body", "Another Font", 11, "#333333"));
        Assert.Equal(1, factory.SharedStyleCount);
    }

    [Fact]
    public void Interpreter_GivesAndHigherPrecedenceThanOr()
    {
        StyleDefinition style = new("Body", "Noto Serif", 11, "#333333");
        ReportSection publicExternal = CreateSection("PUBLIC", Audience.External, style, "public");
        ReportSection financeInternal = CreateSection("FINANCE-IN", Audience.Internal, style, "finance");
        ReportSection financeExternal = CreateSection("FINANCE-OUT", Audience.External, style, "finance");
        ISectionExpression expression = new SectionFilterParser().Parse(
            "tag = public OR tag = finance AND audience = internal");

        Assert.True(expression.Interpret(publicExternal));
        Assert.True(expression.Interpret(financeInternal));
        Assert.False(expression.Interpret(financeExternal));
    }

    [Fact]
    public void Decorators_ApplyInOrderAndSignatureCoversWatermarkedContent()
    {
        StyleDefinition style = new("Body", "Noto Serif", 11, "#333333");
        ReportDocument document = CreateDocument(CreateSection("S-1", Audience.External, style, "public"));
        IArtifactProducer producer = new AuditDecorator(
            new SignatureDecorator(
                new WatermarkDecorator(new ResponsiveHtmlRenderer(), "DRAFT")),
            "teacher");

        RenderedArtifact artifact = producer.Produce(document);
        int watermarkIndex = artifact.Content.IndexOf("<!-- WATERMARK: DRAFT -->", StringComparison.Ordinal);
        int signatureIndex = artifact.Content.IndexOf("\n<!-- SIGNATURE:", StringComparison.Ordinal);
        int auditIndex = artifact.Content.IndexOf("\n<!-- AUDIT:", StringComparison.Ordinal);
        string signedContent = artifact.Content[..signatureIndex];
        string expectedSignature = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(signedContent)))[..16];

        Assert.True(watermarkIndex >= 0);
        Assert.True(signatureIndex > watermarkIndex);
        Assert.True(auditIndex > signatureIndex);
        Assert.Equal(expectedSignature, artifact.Metadata["Signature"]);
        Assert.Equal("teacher", artifact.Metadata["AuditActor"]);
    }

    [Fact]
    public void WebComponentFamily_ProducesAConsistentWebPackage()
    {
        StyleDefinition style = new("Body", "Noto Serif", 11, "#333333");
        ReportDocument document = CreateDocument(CreateSection("S-1", Audience.External, style, "public"));
        IOutputComponentFactory factory = new WebOutputComponentFactory();

        RenderedArtifact artifact = factory.CreateRenderer().Produce(document);
        PublicationPackage package = factory.CreatePackager().Package(document, artifact);

        Assert.Equal(OutputChannel.Web, factory.Channel);
        Assert.Equal(factory.FamilyName, package.ComponentFamily);
        Assert.Equal("html", artifact.Format);
        Assert.EndsWith(".sitepkg", package.PackageName, StringComparison.Ordinal);
        Assert.Contains("index.html", package.Files);
    }

    [Fact]
    public void PrintComponentFamily_ProducesAConsistentPrintPackage()
    {
        StyleDefinition style = new("Body", "Noto Serif", 11, "#333333");
        ReportDocument document = CreateDocument(CreateSection("S-1", Audience.External, style, "public"));
        IOutputComponentFactory factory = new PrintOutputComponentFactory();

        RenderedArtifact artifact = factory.CreateRenderer().Produce(document);
        PublicationPackage package = factory.CreatePackager().Package(document, artifact);

        Assert.Equal(OutputChannel.Print, factory.Channel);
        Assert.Equal(factory.FamilyName, package.ComponentFamily);
        Assert.Equal("print-text", artifact.Format);
        Assert.EndsWith(".printpkg", package.PackageName, StringComparison.Ordinal);
        Assert.Contains("report.prn", package.Files);
    }

    [Fact]
    public void Packager_RejectsArtifactFromAnotherComponentFamily()
    {
        StyleDefinition style = new("Body", "Noto Serif", 11, "#333333");
        ReportDocument document = CreateDocument(CreateSection("S-1", Audience.External, style, "public"));
        RenderedArtifact printArtifact = new PrintOutputComponentFactory()
            .CreateRenderer()
            .Produce(document);

        Assert.Throws<InvalidOperationException>(() =>
            new WebOutputComponentFactory().CreatePackager().Package(document, printArtifact));
    }

    private static ReportDocument CreateDocument(params ReportSection[] sections) =>
        new(
            "TEMPLATE",
            "Template",
            "Training",
            new SectionCollection(sections),
            Array.Empty<KeyValuePair<string, string>>());

    private static ReportSection CreateSection(
        string id,
        Audience audience,
        StyleDefinition style,
        params string[] tags) =>
        new(
            id,
            "Original Section",
            "Section body",
            audience,
            estimatedPages: 1,
            style,
            tags);
}
