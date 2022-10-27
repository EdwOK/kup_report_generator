using System.Diagnostics;
using FluentResults;
using Microsoft.Win32;

namespace KUPReportGenerator.Generators;

public class GoogleChromePdfGenerator : IPdfGenerator
{
    private const string ChromeRegistryKey = "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Google Chrome";
    private const string ChromeRegistryValue = "InstallLocation";
    private const string ChromeExecutable = "chrome.exe";
    private const string ChromeArguments = "--headless --disable-gpu --print-to-pdf-no-header --print-to-pdf=\"{0}\" \"{1}\"";

    public async Task<Result> HtmlToPdfAsync(string htmlPath, string pdfPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(htmlPath))
        {
            return Result.Fail($"{nameof(htmlPath)} can't be null or empty.");
        }

        if (string.IsNullOrEmpty(pdfPath))
        {
            return Result.Fail($"{nameof(pdfPath)} can't be null or empty.");
        }

        var error = new Error($"Error while generating PDF file. {htmlPath}");

        var chromePath = GetChromePath();
        if (chromePath.IsFailed)
        {
            return Result.Fail(error).WithErrors(chromePath.Errors);
        }

        try
        {
            var chromeArguments = GetChromeArguments(htmlPath, pdfPath);
            var chromeProcessInfo = GetChromeProcessInfo(chromePath.Value, chromeArguments);

            using var chromeProcess = Process.Start(chromeProcessInfo);
            if (chromeProcess is null)
            {
                return Result.Fail(error).WithError("Google Chrome process can't start.");
            }

            await chromeProcess.WaitForExitAsync(cancellationToken);
            if (chromeProcess.ExitCode is not 0)
            {
                return Result.Fail(error).WithError($"Google Chrome process exited with code {chromeProcess.ExitCode}.");
            }
        }
        catch (Exception exc)
        {
            return Result.Fail(error.CausedBy(exc));
        }

        return Result.Ok();
    }

    private static string GetChromeArguments(string htmlPath, string pdfPath) =>
        string.Format(ChromeArguments, pdfPath, htmlPath);

    private static ProcessStartInfo GetChromeProcessInfo(string chromePath, string chromeArguments) =>
        new(chromePath, chromeArguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

    private static Result<string> GetChromePath()
    {
        var error = new Error("Google Chrome isn't installed. Please download and install it.");

        var chromePath = Registry.GetValue(ChromeRegistryKey, ChromeRegistryValue, null) as string;
        if (string.IsNullOrEmpty(chromePath))
        {
            return Result.Fail(error);
        }

        chromePath = Path.Combine(chromePath, ChromeExecutable);
        if (!File.Exists(chromePath))
        {
            return Result.Fail(error);
        }

        return chromePath;
    }
}