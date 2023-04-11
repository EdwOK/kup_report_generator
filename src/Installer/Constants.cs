using System.Runtime.InteropServices;

namespace KUPReportGenerator.Installer;

public static class Constants
{
    public static readonly string CurrentDirectory = Directory.GetCurrentDirectory();

    public static readonly string DownloadDirectory = Path.Combine(CurrentDirectory, "Downloads");

    public const string RepositoryOwner = "EdwOK";

    public const string Repository = "kup_report_generator";

    public static readonly OSPlatform CurrentOSPlatform = OSPlatform.Create(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OSPlatform.Windows.ToString()
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? OSPlatform.OSX.ToString()
                : OSPlatform.Linux.ToString());
}