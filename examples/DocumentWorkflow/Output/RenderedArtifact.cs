namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Output;

public sealed class RenderedArtifact
{
    private readonly SortedDictionary<string, string> _metadata;
    private readonly List<string> _auditTrail;

    public RenderedArtifact(
        string format,
        string content,
        IEnumerable<KeyValuePair<string, string>> metadata,
        IEnumerable<string> auditTrail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(auditTrail);

        Format = format;
        Content = content;
        _metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in metadata)
        {
            _metadata[pair.Key] = pair.Value;
        }

        _auditTrail = auditTrail.ToList();
    }

    public string Format { get; }

    public string Content { get; }

    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    public IReadOnlyList<string> AuditTrail => _auditTrail;

    public RenderedArtifact AddDecoration(
        string contentSuffix,
        string metadataKey,
        string metadataValue,
        string auditEntry)
    {
        ArgumentNullException.ThrowIfNull(contentSuffix);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditEntry);

        var metadata = new SortedDictionary<string, string>(_metadata, StringComparer.Ordinal)
        {
            [metadataKey] = metadataValue
        };
        var auditTrail = new List<string>(_auditTrail) { auditEntry };
        return new RenderedArtifact(Format, Content + contentSuffix, metadata, auditTrail);
    }
}

public sealed record PublicationPackage(
    OutputChannel Channel,
    string PackageName,
    string ComponentFamily,
    string PackagerName,
    string Payload,
    IReadOnlyList<string> Files);
