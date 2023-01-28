using System.Runtime.InteropServices;

namespace KUPReportGenerator.Helpers;

public static class EnvironmentUtils
{
    public static bool IsWindowsPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsLinuxPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static bool IsMacOSPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}