namespace Helpers.Github;

public sealed record AppRelease
{
    public required string Version { get; init; }

    public required string Description { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required IEnumerable<AppReleaseAsset> Assets { get; init; }
}