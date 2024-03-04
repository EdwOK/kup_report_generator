using System.Reflection;
using System.Runtime.InteropServices;
using CliWrap;
using Helpers.Github;

namespace Helpers;

public static class AppHelper
{
    public static string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }

    public static OSPlatform CurrentOsPlatform => OSPlatform.Create(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OSPlatform.Windows.ToString()
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? OSPlatform.OSX.ToString()
                : OSPlatform.Linux.ToString());

    public static async Task<Result> CheckPrerequisites(CancellationToken cancellationToken)
    {
        var gitVersion = await Result.Try(GetGitVersion);
        if (gitVersion.IsFailed)
        {
            return Result.Fail(
                "Please ensure Git is installed and configured correctly, more information https://github.com/EdwOK/kup_report_generator?tab=readme-ov-file#prerequisites");
        }

        var credentialManagerVersion = await Result.Try(GetCredentialManagerVersion);
        if (credentialManagerVersion.IsFailed)
        {
            return Result.Fail(
                "Please ensure Git Credential Manager is installed and configured correctly, more information https://github.com/EdwOK/kup_report_generator?tab=readme-ov-file#prerequisites");
        }

        return Result.Ok();

        async Task<CommandResult> GetGitVersion() =>
            await Cli.Wrap("git")
                .WithArguments("--version")
                .ExecuteAsync(cancellationToken);

        async Task<CommandResult> GetCredentialManagerVersion() =>
            await Cli.Wrap("git")
                .WithArguments("credential-manager --version")
                .ExecuteAsync(cancellationToken);
    }

    public static async Task<AppRelease?> CheckAppVersionForUpdate(string appVersion, CancellationToken cancellationToken)
    {
        var repository = new GithubRepository(AppConstants.RepositoryOwner, AppConstants.RepositoryName);

        var latestRelease = await repository.GetLatestRelease(cancellationToken);
        if (latestRelease.IsFailed)
        {
            return null;
        }

        return appVersion != latestRelease.Value.Version ? latestRelease.Value : null;
    }
}