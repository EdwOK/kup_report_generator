using System.Runtime.InteropServices;

namespace KUPReportGenerator.Installer;

public record ReleaseAsset
{
    public required OSPlatform OSPlatform { get; init; }

    public required string FileName { get; init; }

    public required string DownloadUrl { get; init; }
}