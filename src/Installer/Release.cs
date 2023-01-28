namespace KUPReportGenerator.Installer;

public record Release
{
    public required string Version { get; init; }

    public required string Description { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required IEnumerable<ReleaseAsset> Assets { get; init; }
}