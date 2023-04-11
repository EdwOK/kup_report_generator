using System.Runtime.InteropServices;
using KUPReportGenerator.Installer;

namespace Installer;

internal class GithubRepository
{
    private readonly Octokit.IGitHubClient _client;
    private readonly string _url;
    private readonly string _owner;

    public GithubRepository(string owner, string url)
    {
        _owner = owner;
        _url = url;
        _client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue(nameof(KUPReportGenerator)));
    }

    public async Task<IEnumerable<Release>> GetReleases(CancellationToken cancellationToken)
    {
        var repoReleases = await _client.Repository.Release.GetAll(_owner, _url)
            .WaitAsync(cancellationToken);

        var releases = new List<Release>();

        foreach (var release in repoReleases)
        {
            var assets = release.Assets.Select(a =>
            {
                var osPlatform = GetOSPlatform(a.Name);
                if (osPlatform is null)
                {
                    return null;
                }

                return new ReleaseAsset
                {
                    FileName = a.Name,
                    OSPlatform = (OSPlatform)osPlatform,
                    DownloadUrl = a.BrowserDownloadUrl,
                };
            })
            .Where(a => a is not null)
            .ToArray();

            if (!assets.Any())
            {
                continue;
            }

            releases.Add(new Release
            {
                Version = release.TagName,
                Description = release.Body,
                CreatedAt = release.CreatedAt,
                PublishedAt = release.PublishedAt,
                Assets = assets!
            });
        }

        return releases;
    }

    static OSPlatform? GetOSPlatform(string name)
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

        return null;
    }
}