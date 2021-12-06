using FluentResults;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KUPReportGenerator
{
    internal static class CommandLineHelper
    {
        public static bool IsWindowsPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

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

            var process = Process.Start(processStartInfo);
            if (process is null)
            {
                return Result.Fail("Process.Start failed to return a non-null process");
            }

            await process.WaitForExitAsync(cancellationToken);

            return Result.Ok(process);
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
    }
}
