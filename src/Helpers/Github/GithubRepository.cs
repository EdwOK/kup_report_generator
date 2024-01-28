using System.Runtime.InteropServices;
using Octokit;

namespace Helpers.Github;

public sealed class GithubRepository(string owner, string name)
{
    private readonly IGitHubClient _client = new GitHubClient(new ProductHeaderValue("KUPReportGenerator"));

    public async Task<Result<AppRelease>> GetLatestRelease(CancellationToken cancellationToken)
    {
        var error = new Error("Couldn't retrieve latest app version.");

        var latestRepoRelease = await Result.Try(() =>
            _client.Repository.Release.GetLatest(owner, name).WaitAsync(cancellationToken));
        if (latestRepoRelease.IsFailed)
        {
            return error.CausedBy(latestRepoRelease.Errors);
        }

        var releaseAssets = GetReleaseAssets(latestRepoRelease.Value);
        if (releaseAssets.IsFailed)
        {
            return error.CausedBy(releaseAssets.Errors);
        }

        return new AppRelease
        {
            Version = latestRepoRelease.Value.TagName,
            Description = latestRepoRelease.Value.Body,
            CreatedAt = latestRepoRelease.Value.CreatedAt,
            PublishedAt = latestRepoRelease.Value.PublishedAt,
            Assets = releaseAssets.Value
        };
    }

    public async Task<Result<IEnumerable<AppRelease>>> GetReleases(CancellationToken cancellationToken)
    {
        var error = new Error("Couldn't retrieve app versions.");

        var repoReleases = await Result.Try(() =>
            _client.Repository.Release.GetAll(owner, name).WaitAsync(cancellationToken));
        if (repoReleases.IsFailed)
        {
            return error.CausedBy(repoReleases.Errors);
        }

        var releases = new List<AppRelease>();

        foreach (var release in repoReleases.Value)
        {
            var releaseAssets = GetReleaseAssets(release);
            if (releaseAssets.IsFailed)
            {
                continue;
            }

            releases.Add(new AppRelease
            {
                Version = release.TagName,
                Description = release.Body,
                CreatedAt = release.CreatedAt,
                PublishedAt = release.PublishedAt,
                Assets = releaseAssets.Value
            });
        }

        return releases;
    }

    private static Result<AppReleaseAsset[]> GetReleaseAssets(Release release)
    {
        var assets = release.Assets
            .Where(asset => asset.ContentType == "binary/octet-stream")
            .Select(asset =>
            {
                var osPlatform = GetOsPlatform(asset.Name);
                if (osPlatform.IsFailed)
                {
                    return osPlatform.ToResult();
                }

                return Result.Ok(new AppReleaseAsset
                {
                    FileName = asset.Name,
                    OsPlatform = osPlatform.Value,
                    DownloadUrl = asset.BrowserDownloadUrl,
                });
            })
            .ToArray();

        if (assets.Any(a => a.IsFailed))
        {
            return Result.Fail($"No assets for release: {release.TagName}!");
        }

        return assets.Select(asset => asset.Value).ToArray();
    }

    private static Result<OSPlatform> GetOsPlatform(string name)
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

        return Result.Fail($"Not supported for OS: '{name}'.");
    }
}