namespace Installer;

internal static class Constants
{
    public static readonly string CurrentDirectory = Directory.GetCurrentDirectory();

    public static readonly string DownloadDirectory = Path.Combine(CurrentDirectory, "Downloads");
}