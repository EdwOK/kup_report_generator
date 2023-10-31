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

        using var browserFetcher = new BrowserFetcher(SupportedBrowser.Chrome);

        var installedBrowser = browserFetcher.GetInstalledBrowsers()
            .FirstOrDefault(b => b.Browser is SupportedBrowser.Chrome);

        installedBrowser ??= await browserFetcher.DownloadAsync(BrowserTag.Stable);

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = installedBrowser.GetExecutablePath(),
            Headless = true,
        });

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html.Value);
        var pdf = await page.PdfDataAsync();
        await FileHelper.SaveAsync(Constants.PdfReportFilePath, pdf, cancellationToken);

        return Result.Ok();
    }
}