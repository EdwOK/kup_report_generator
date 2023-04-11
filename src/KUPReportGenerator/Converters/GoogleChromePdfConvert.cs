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
            return error.CausedBy(html.Errors);
        }

        using var browserFetcher = new BrowserFetcher();

        var canDownload = await Result.Try(() => browserFetcher.CanDownloadAsync(BrowserFetcher.DefaultChromiumRevision)
            .WaitAsync(cancellationToken));
        if (canDownload.IsFailed)
        {
            return new Error("Unable to download Google Chrome.").CausedBy(canDownload.Errors);
        }

        var revisionInfo = await Result.Try(() => browserFetcher.DownloadAsync().WaitAsync(cancellationToken));
        if (revisionInfo.IsFailed)
        {
            return new Error("Error while downloading Google Chrome.").CausedBy(revisionInfo.Errors);
        }

        if (revisionInfo.ValueOrDefault?.Downloaded is false)
        {
            return new Error("Error while downloading Google Chrome.");
        }

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = revisionInfo.Value.ExecutablePath,
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
        await FileHelper.SaveAsync(Constants.PdfReportFilePath, pdf, cancellationToken);

        return Result.Ok();
    }
}