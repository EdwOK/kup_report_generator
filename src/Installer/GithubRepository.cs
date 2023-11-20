using System.Runtime.InteropServices;
using KUPReportGenerator.Helpers.TaskProgress;
using KUPReportGenerator.Installer;
using Spectre.Console;

namespace Installer;

internal class GithubRepository(string owner, string url, IProgressContext progressContext)
{
    private readonly Octokit.IGitHubClient _client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue(nameof(KUPReportGenerator)));

    public async Task<IEnumerable<Release>> GetReleases(CancellationToken cancellationToken)
    {
        var repoReleasesTask = progressContext.AddTask("Getting app releases");
        repoReleasesTask.Increment(50.0);
        var repoReleases = await _client.Repository.Release.GetAll(owner, url)
            .WaitAsync(cancellationToken);
        repoReleasesTask.Increment(25.0);

        var releases = new List<Release>();

        foreach (var release in repoReleases)
        {
            var assets = release.Assets.Select(releaseAsset =>
            {
                var osPlatform = GetOSPlatform(releaseAsset.Name);
                if (osPlatform.IsFailed)
                {
                    return osPlatform.ToResult();
                }

                return Result.Ok(new ReleaseAsset
                {
                    FileName = releaseAsset.Name,
                    OSPlatform = osPlatform.Value,
                    DownloadUrl = releaseAsset.BrowserDownloadUrl,
                });
            });

            if (assets.Any(a => a.IsFailed))
            {
                continue;
            }

            releases.Add(new Release
            {
                Version = release.TagName,
                Description = release.Body,
                CreatedAt = release.CreatedAt,
                PublishedAt = release.PublishedAt,
                Assets = assets.Select(a => a.Value)
            });
        }

        repoReleasesTask.Increment(25.0);
        return releases;
    }

    private static Result<OSPlatform> GetOSPlatform(string name)
    {
        if (name.Contains("win", StringComparison.InvariantCultureIgnoreCase))
        {
            return OSPlatform.Windows;
        }
        else if (name.Contains("linux", StringComparison.InvariantCultureIgnoreCase))
        {
            return OSPlatform.Linux;
        }
        else if (name.Contains("macos", StringComparison.InvariantCultureIgnoreCase))
        {
            return OSPlatform.OSX;
        }

        return Result.Fail($"Not supported for {name} OS.");
    }
}