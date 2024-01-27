using Helpers;

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

        var error = new Error($"Error while generating PDF file: {htmlPath}");

        var html = await FileHelper.ReadAsync(htmlPath, cancellationToken);
        if (html.IsFailed)
        {
            return error.CausedBy(html.Errors);
        }

        var savePdf = await Result.Try(() => SavePdf(html.Value, cancellationToken), error.CausedBy);
        return savePdf;
    }

    private static async Task SavePdf(string fileContent, CancellationToken cancellationToken)
    {
        await using var browser = await BrowserLauncher.Launch();
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(fileContent);
        var pdf = await page.PdfDataAsync();
        await FileHelper.SaveAsync(Constants.PdfReportFilePath, pdf, cancellationToken);
    }
}