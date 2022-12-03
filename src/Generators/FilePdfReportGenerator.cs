using FluentResults;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

internal class FilePdfReportGenerator : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        var pdfGenerator = GoogleChromePdfGenerator.Create();
        if (pdfGenerator.IsSuccess)
        {
            var savePdfReportTask = progressContext.AddTask("[green]Saving pdf report in a file.[/]");
            savePdfReportTask.Increment(50.0);
            var pdfResult = await pdfGenerator.Value.HtmlToPdfAsync(Constants.HtmlReportFilePath, Constants.PdfReportFilePath,
                cancellationToken);
            savePdfReportTask.Increment(50.0);
            return pdfResult;
        }

        return Result.Ok();
    }
}