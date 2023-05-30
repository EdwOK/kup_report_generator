using System.IO.Compression;
using System.Runtime.InteropServices;
using FluentResults;

namespace KUPReportGenerator.Installer;

public class AppInstaller
{
    public async Task<Result> Install(Release release, OSPlatform osPlatform, CancellationToken cancellationToken)
    {
        var releaseAsset = release.Assets.FirstOrDefault(a => a.OSPlatform.Equals(osPlatform));
        if (releaseAsset is null)
        {
            return Result.Fail($"No release asset found for the specified {osPlatform} OS platform.");
        }

        var downloadDirectoryPath = CreateDownloadDirectory(Constants.DownloadDirectory);
        var archivePath = Path.Combine(downloadDirectoryPath, $"{release.Version}-{releaseAsset!.FileName}");

        var downloadedArchive = await DownloadFile(releaseAsset.DownloadUrl, archivePath, cancellationToken);

        var extractPath = ExtractArchive(downloadedArchive, downloadDirectoryPath);
        if (extractPath.IsFailed)
        {
            return extractPath.ToResult();
        }

        UpdateAppFiles(Constants.CurrentDirectory, extractPath.Value);

        return Result.Ok();
    }

    private static string CreateDownloadDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }

        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void UpdateAppFiles(string destinationPath, string extractPath)
    {
        var newFiles = Directory.GetFiles(extractPath);

        foreach (var newFilePath in newFiles)
        {
            var newFileName = Path.GetFileName(newFilePath);

            var destFilePath = Path.Combine(destinationPath, newFileName);
            if (File.Exists(destFilePath))
            {
                var oldFilePath = Path.Combine(extractPath, $"{newFileName}.old");
                File.Move(destFilePath, oldFilePath, true);
            }

            File.Move(newFilePath, destFilePath, true);
        }
    }

    private static Result<string> ExtractArchive(string archivePath, string extractPath)
    {
        var archiveExtension = Path.GetExtension(archivePath);
        return archiveExtension switch
        {
            ".zip" => Result.Try(() =>
            {
                ZipFile.ExtractToDirectory(archivePath, extractPath, true);
                File.Delete(archivePath);
                return extractPath;
            }),
            _ => Result.Fail($"No support for {archiveExtension}")
        };
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
}