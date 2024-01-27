using System.Reflection;
using System.Runtime.InteropServices;
using Helpers.Releases;

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