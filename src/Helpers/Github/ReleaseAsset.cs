using System.Runtime.InteropServices;

namespace Helpers.Github;

public sealed record AppReleaseAsset
{
    public required OSPlatform OsPlatform { get; init; }

    public required string FileName { get; init; }

    public required string DownloadUrl { get; init; }
}