using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Helpers.Releases;

namespace Installer;

public class AppInstaller
{
    public static async Task<Result> Install(AppRelease release, OSPlatform osPlatform, CancellationToken cancellationToken)
    {
        var appReleaseAsset = GetAppReleaseAsset(release, osPlatform);
        if (appReleaseAsset.IsFailed)
        {
            return appReleaseAsset.ToResult();
        }

        var appArchivePath = await DownloadAppArchive(Constants.DownloadDirectory, appReleaseAsset.Value, 
            cancellationToken);
        if (appArchivePath.IsFailed)
        {
            return appArchivePath.ToResult();
        }

        var appPath = ExtractAppArchive(appArchivePath.Value);
        if (appPath.IsFailed)
        {
            return appPath.ToResult();
        }

        var updateAppFiles = UpdateAppFiles(Constants.CurrentDirectory, appPath.Value);
        return updateAppFiles;
    }

    private static async Task<Result<string>> DownloadAppArchive(string downloadDirectoryPath, AppReleaseAsset releaseAsset,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(downloadDirectoryPath))
        {
            Directory.Delete(downloadDirectoryPath, true);
        }
        Directory.CreateDirectory(downloadDirectoryPath);

        var downloadFilePath = Path.Combine(downloadDirectoryPath, releaseAsset.FileName);
        var downloadArchivePath = await DownloadFile(releaseAsset.DownloadUrl, downloadFilePath, 
            cancellationToken);
        if (downloadArchivePath.IsFailed)
        {
            return downloadArchivePath.ToResult();
        }

        return Result.Ok(downloadArchivePath.Value);
    }

    private static Result<AppReleaseAsset> GetAppReleaseAsset(AppRelease release, OSPlatform osPlatform)
    {
        var releaseAsset = release.Assets.FirstOrDefault(a => a.OsPlatform.Equals(osPlatform));
        if (releaseAsset is null)
        {
            return Result.Fail($"No release asset found for the specified OS '{osPlatform}'.");
        }

        return Result.Ok(releaseAsset);
    }

    private static Result UpdateAppFiles(string oldAppPath, string newAppPath)
    {
        var oldFiles = Directory.GetFiles(oldAppPath);
        foreach (var oldFilePath in oldFiles)
        {
            var oldFileName = Path.GetFileName(oldFilePath);
            var destFilePath = Path.Combine(oldAppPath, $".{oldFileName}.old");
            File.Move(oldFilePath, destFilePath, true);
        }

        var newFiles = Directory.GetFiles(newAppPath);
        foreach (var newFilePath in newFiles)
        {
            var newFileName = Path.GetFileName(newFilePath);
            var destFilePath = Path.Combine(oldAppPath, newFileName);
            File.Move(newFilePath, destFilePath, true);
        }

        return Result.Ok();
    }

    private static Result<string> ExtractAppArchive(string archivePath)
    {
        var archiveExtension = Path.GetExtension(archivePath);
        var archiveDirectory = Path.GetDirectoryName(archivePath) ?? Constants.DownloadDirectory;

        return archiveExtension switch
        {
            ".zip" => Result.Try(() =>
            {
                ZipFile.ExtractToDirectory(archivePath, archiveDirectory, true);
                File.Delete(archivePath);
                return archiveDirectory;
            }),
            ".tar.gz" => Result.Try(() =>
            {
                using var archiveStream = File.Open(archivePath, FileMode.Open);
                using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzipStream, archiveDirectory, true);
                File.Delete(archivePath);
                return archiveDirectory;
            }),
            _ => Result.Fail($"No support for '{archiveExtension}'.")
        };
    }

    private static async Task<Result<string>> DownloadFile(string fileUrl, string filePath,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        await using var downloadStream = await httpClient.GetStreamAsync(fileUrl, cancellationToken);
        await using var fileStream = File.Create(filePath);
        await downloadStream.CopyToAsync(fileStream, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
        return Result.Ok(filePath);
    }
}