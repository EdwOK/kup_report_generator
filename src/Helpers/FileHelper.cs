namespace Helpers;

public static class FileHelper
{
    public static bool AnyFiles(string directory)
    {
        return Directory.Exists(directory) && Directory.GetFiles(directory).Any();
    }

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
        return fileContent;
    }

    public static async Task SaveAsync(string filePath, byte[] data, CancellationToken cancellationToken)
    {
        EnsureFileDirectoryExists(filePath);

        await using var file = File.Create(filePath);
        await file.WriteAsync(data, cancellationToken);
        await file.FlushAsync(cancellationToken);
    }

    private static void EnsureFileDirectoryExists(string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath!);
        }
    }
}