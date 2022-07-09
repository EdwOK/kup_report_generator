using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentResults;

namespace KUPReportGenerator.Helpers;

internal static class EnvironmentUtils
{
    public static async Task<Result<Process>> RunCommandAsync(string command, string workingDirectory = "", bool redirectIo = true,
        CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = GetSystemShell(),
            Arguments = IsWindowsPlatform() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardInput = redirectIo,
            RedirectStandardOutput = redirectIo,
            RedirectStandardError = redirectIo,
            UseShellExecute = false,
            CreateNoWindow = redirectIo,
            WorkingDirectory = workingDirectory
        };

        var process = Result.Try(() => Process.Start(processStartInfo));
        if (process.IsFailed)
        {
            return process.ToResult();
        }

        if (process.Value is null)
        {
            return Result.Fail("Process.Start failed to return a non-null process.");
        }

        var waitForExitAsync = await Result.Try(() => process.Value.WaitForExitAsync(cancellationToken));
        if (waitForExitAsync.IsFailed)
        {
            return waitForExitAsync;
        }

        return Result.Ok(process.Value);
    }

    private static string GetSystemShell()
    {
        if (TryGetEnvironmentVariable("COMSPEC", out var comspec))
        {
            return comspec!;
        }

        if (TryGetEnvironmentVariable("SHELL", out var shell))
        {
            return shell!;
        }

        return IsWindowsPlatform() ? "cmd.exe" : "/bin/sh";
    }

    private static bool TryGetEnvironmentVariable(string variable, out string? value)
    {
        value = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrEmpty(value);
    }

    public static bool IsWindowsPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsLinuxPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
}