using System.IO.Compression;
using System.Runtime.InteropServices;
using FluentResults;

namespace KUPReportGenerator;

public class UpdateManager
{
    private readonly Octokit.IGitHubClient _client;

    public UpdateManager() =>
        _client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue(nameof(KUPReportGenerator)));

    public async Task<Result<IEnumerable<Release>>> GetReleases(CancellationToken cancellationToken)
    {
        var repoReleases = await Result.Try(() => _client.Repository.Release.GetAll(Constants.RepositoryOwner, Constants.Repository)
            .WaitAsync(cancellationToken));
        if (repoReleases.IsFailed)
        {
            return repoReleases.ToResult();
        }

        var releases = new List<Release>();

        foreach (var release in repoReleases.Value)
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
            .Where(a => a != null)
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

        static OSPlatform? GetOSPlatform(string name)
        {
            if (name.Contains("win"))
            {
                return OSPlatform.Windows;
            }
            else if (name.Contains("linux"))
            {
                return OSPlatform.Linux;
            }
            else if (name.Contains("macos"))
            {
                return OSPlatform.OSX;
            }

            return null;
        }
    }

    public async Task<Result> Update(Release release, OSPlatform osPlatform, CancellationToken cancellationToken)
    {
        var releaseAsset = release.Assets.FirstOrDefault(a => a.OSPlatform.Equals(osPlatform));
        if (releaseAsset is null)
        {
            return Result.Fail("No release asset found for the current OS platform.");
        }

        var fileName = Path.Combine(Path.GetTempPath(), releaseAsset!.FileName);
        var tempFile = await Result.Try(() => DownloadFile(releaseAsset.DownloadUrl, fileName, cancellationToken));

        return Result.Ok();
    }

    private static string UnzipFile(string zipPath, string extractPath)
    {
        ZipFile.ExtractToDirectory(zipPath, extractPath);
        return extractPath;
    }

    private static async Task<string> DownloadFile(string fileUrl, string filePath, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var downloadStream = await httpClient.GetStreamAsync(fileUrl, cancellationToken);
        using var fileStream = File.Create(filePath);
        await downloadStream.CopyToAsync(fileStream, cancellationToken);
        fileStream.Flush();
        return filePath;
    }
}

public record Release
{
    public required string Version { get; init; }

    public required string Description { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public required IEnumerable<ReleaseAsset> Assets { get; init; }
}

public record ReleaseAsset
{
    public required OSPlatform OSPlatform { get; init; }

    public required string FileName { get; init; }

    public required string DownloadUrl { get; init; }
}