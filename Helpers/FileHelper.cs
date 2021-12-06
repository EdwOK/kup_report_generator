using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using KUPReportGenerator.Helpers;

namespace KUPReportGenerator
{
    internal static class FileHelper
    {
        public static async Task<Result<string>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return Result.Fail($"{filePath} could not be found.");
                }

                await using var file = File.OpenRead(filePath);
                using var htmlReader = new StreamReader(file);
                var fileContent = await htmlReader.ReadToEndAsync().WithCancellation(cancellationToken);
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
                await file.FlushAsync().WithCancellation(cancellationToken);
                return Result.Ok();
            }
            catch (Exception exc)
            {
                return Result.Fail(new Error($"Failed with creation {filePath}.").CausedBy(exc));
            }
        }
    }
}