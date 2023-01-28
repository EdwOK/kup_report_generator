using System.Reflection;
using FluentResults;
using GitCredentialManager;
using GitCredentialManager.Interop.MacOS;
using GitCredentialManager.Interop.Windows;

namespace KUPReportGenerator.Helpers;

public static class CredentialManager
{
    public static Result<ICredentialStore> Create(string? @namespace = default)
    {
        if (EnvironmentUtils.IsWindowsPlatform())
        {
            return Result.Try<ICredentialStore>(() => new WindowsCredentialManager(@namespace));
        }
        else if (EnvironmentUtils.IsLinuxPlatform())
        {
            return Result.Try<ICredentialStore>(() => new CommandContext(GetApplicationPath()).CredentialStore);
        }
        else if (EnvironmentUtils.IsMacOSPlatform())
        {
            return Result.Try<ICredentialStore>(() => new MacOSKeychain(@namespace));
        }
        else
        {
            return Result.Fail("Current OS platform not supported.");
        }
    }

    private static string GetApplicationPath()
    {
        var isSingleFile = string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location);

        var args = Environment.GetCommandLineArgs();
        var candidatePath = args[0];

        if (!isSingleFile && Path.HasExtension(candidatePath))
        {
            return Path.ChangeExtension(candidatePath, null);
        }

        return candidatePath;
    }
}