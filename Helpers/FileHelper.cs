using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;

namespace KUPReportGenerator.Helpers;

internal static class FileHelper
{
    public static Result<bool> Exists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                return Result.Ok();
            }

            return Result.Fail($"{filePath} could not be found.");
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed check for existence of {filePath}.").CausedBy(exc));
        }
    }

    public static async Task<Result<string>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileExists = Exists(filePath);
            if (fileExists.IsFailed)
            {
                return fileExists.ToResult();
            }

            await using var file = File.OpenRead(filePath);
            using var htmlReader = new StreamReader(file);
            var fileContent = await htmlReader.ReadToEndAsync().WaitAsync(cancellationToken);
            return Result.Ok(fileContent);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed with reading {filePath}.").CausedBy(exc));
        }
    }

    public static async Task<Result> SaveAsync(string filePath, string text, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var file = File.CreateText(filePath);
            await file.WriteAsync(text.ToCharArray(), cancellationToken);
            await file.FlushAsync().WaitAsync(cancellationToken);
            return Result.Ok();
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed with creation {filePath}.").CausedBy(exc));
        }
    }
}