using FluentResults;
using KUPReportGenerator.Converters;
using KUPReportGenerator.Report;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

internal class FilePdfReportGenerator : IReportGenerator
{
    private IPdfConverter _pdfConverter;

    public FilePdfReportGenerator(IPdfConverter pdfConverter) =>
        _pdfConverter = pdfConverter;

    public async Task<Result> Generate(ReportGeneratorContext reportContext, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        var savePdfReportTask = progressContext.AddTask("[green]Saving pdf report in a file.[/]");
        savePdfReportTask.Increment(50.0);
        var pdfResult = await _pdfConverter.HtmlToPdfAsync(Constants.HtmlReportFilePath, Constants.PdfReportFilePath,
            cancellationToken);
        savePdfReportTask.Increment(50.0);
        return pdfResult;
    }
}