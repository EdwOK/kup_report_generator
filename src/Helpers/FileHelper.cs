using FluentResults;

namespace KUPReportGenerator.Helpers;

public static class FileHelper
{
    public static async Task<Result<string>> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileExists = File.Exists(filePath);
        if (!fileExists)
        {
            return Result.Fail($"{filePath} couldn't be found.");
        }

        await using var file = File.OpenRead(filePath);
        using var fileReader = new StreamReader(file);
        var fileContent = await fileReader.ReadToEndAsync(cancellationToken);
        return Result.Ok(fileContent);
    }

    public static async Task SaveAsync(string filePath, byte[] data, CancellationToken cancellationToken)
    {
        await using var file = File.Create(filePath);
        await file.WriteAsync(data, cancellationToken);
        await file.FlushAsync(cancellationToken);
    }
}