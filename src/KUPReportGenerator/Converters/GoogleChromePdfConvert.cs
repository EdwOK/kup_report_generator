using FluentResults;
using KUPReportGenerator.Helpers;
using PuppeteerSharp;

namespace KUPReportGenerator.Converters;

public class GoogleChromePdfConvert : IPdfConverter
{
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

        var html = await FileHelper.ReadAsync(htmlPath, cancellationToken);
        if (html.IsFailed)
        {
            return html.ToResult().WithError(error);
        }

        try
        {
            using var browserFetcher = new BrowserFetcher();

            var canDownload = await browserFetcher.CanDownloadAsync(BrowserFetcher.DefaultChromiumRevision);
            if (!canDownload)
            {
                return Result.Fail(error).WithError("Unable to download Google Chrome.");
            }

            var revisionInfo = await browserFetcher.DownloadAsync();
            if (revisionInfo is null || revisionInfo?.Downloaded is false)
            {
                return Result.Fail(error).WithError("Error while downloading Google Chrome.");
            }

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = revisionInfo!.ExecutablePath,
                Headless = true,
                Args = new[] { "--disable-gpu", "--print-to-pdf-no-header" }
            });

            if (!browser.IsConnected)
            {
                return Result.Fail(error).WithError("Google Chrome process can't start.");
            }

            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html.Value);
            var pdf = await page.PdfDataAsync();

            var saveResult = await FileHelper.SaveAsync(Constants.PdfReportFilePath, pdf, cancellationToken);
            return saveResult;
        }
        catch (Exception exc)
        {
            return Result.Fail(error.CausedBy(exc));
        }
    }
}