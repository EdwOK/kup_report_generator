using System.Runtime.InteropServices;

namespace KUPReportGenerator.Helpers;

internal static class EnvironmentUtils
{
    public static bool IsWindowsPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsLinuxPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
}