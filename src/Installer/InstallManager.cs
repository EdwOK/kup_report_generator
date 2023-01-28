using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using FluentResults;

namespace KUPReportGenerator.Installer;

public class InstallManager : IInstallManager
{
    private readonly Octokit.IGitHubClient _client;

    public InstallManager() =>
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

    public async Task<Result> Install(Release release, OSPlatform osPlatform, CancellationToken cancellationToken)
    {
        var releaseAsset = release.Assets.FirstOrDefault(a => a.OSPlatform.Equals(osPlatform));
        if (releaseAsset is null)
        {
            return Result.Fail($"No release asset found for the {osPlatform} OS platform.");
        }

        var init = Result.Try(Initialize);
        if (init.IsFailed)
        {
            return init;
        }

        var archivePath = Path.Combine(Constants.DownloadDirectory, $"{release.Version}-{releaseAsset!.FileName}");

        var downloadedArchive = await Result.Try(() => DownloadFile(releaseAsset.DownloadUrl, archivePath, cancellationToken));
        if (downloadedArchive.IsFailed)
        {
            return downloadedArchive.ToResult();
        }

        var extractPath = ExtractArchive(downloadedArchive.Value, Constants.DownloadDirectory);
        if (extractPath.IsFailed)
        {
            return extractPath.ToResult();
        }

        return Result.Try(() =>
        {
            UpdateAppFiles(extractPath.Value);
            Cleanup();
        });
    }

    private static void UpdateAppFiles(string extractPath)
    {
        var files = Directory.GetFiles(extractPath);

        foreach (var filePath in files)
        {
            var destFilePath = Path.Combine(Constants.CurrentDirectory, Path.GetFileName(filePath));
            File.Move(filePath, destFilePath, true);
        }
    }

    private static Result<string> ExtractArchive(string archivePath, string extractPath)
    {
        var archiveExt = Path.GetExtension(archivePath);
        if (archiveExt == ".zip")
        {
            return Result.Try(() =>
            {
                ZipFile.ExtractToDirectory(archivePath, extractPath, true);
                File.Delete(archivePath);
                return extractPath;
            });
        }
        else
        {
            return Result.Fail($"No support for {archiveExt}");
        }
    }

    private static async Task<string> DownloadFile(string fileUrl, string filePath, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var downloadStream = await httpClient.GetStreamAsync(fileUrl, cancellationToken);
        using var fileStream = File.Create(filePath);
        await downloadStream.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
        return filePath;
    }

    private static void Cleanup()
    {
        Directory.Delete(Constants.DownloadDirectory, true);
    }

    private static void Initialize()
    {
        if (Directory.Exists(Constants.DownloadDirectory))
        {
            Directory.Delete(Constants.DownloadDirectory, true);
        }

        Directory.CreateDirectory(Constants.DownloadDirectory);
    }
}