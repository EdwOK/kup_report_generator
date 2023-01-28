using FluentResults;

namespace KUPReportGenerator.Helpers;

public static class FileHelper
{
    public static Result<bool> Exists(string filePath)
    {
        try
        {
            return Result.FailIf(!File.Exists(filePath), $"{filePath} couldn't be found.");
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed to verify the existence of {filePath}.").CausedBy(exc));
        }
    }

    public static async Task<Result<string>> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileExists = Exists(filePath);
            if (fileExists.IsFailed)
            {
                return fileExists.ToResult();
            }

            await using var file = File.OpenRead(filePath);
            using var fileReader = new StreamReader(file);
            var fileContent = await fileReader.ReadToEndAsync(cancellationToken);
            return Result.Ok(fileContent);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed to read {filePath}.").CausedBy(exc));
        }
    }

    public static async Task<Result> SaveAsync(string filePath, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            await using var file = File.Create(filePath);
            await file.WriteAsync(data, cancellationToken);
            await file.FlushAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed to save {filePath}.").CausedBy(exc));
        }
    }
}