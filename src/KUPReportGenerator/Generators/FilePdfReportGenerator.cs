using Helpers.TaskProgress;
using KUPReportGenerator.Converters;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

internal class FilePdfReportGenerator(IProgressContext progressContext, IPdfConverter pdfConverter) : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var savePdfReportTask = progressContext.AddTask("[green]Saving pdf report in a file.[/]");
        savePdfReportTask.Increment(50.0);
        var htmlToPdf = await pdfConverter.HtmlToPdfAsync(Constants.HtmlReportFilePath, Constants.PdfReportFilePath,
            cancellationToken);
        savePdfReportTask.Increment(50.0);
        return htmlToPdf;
    }
}