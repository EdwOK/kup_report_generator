using FluentResults;
using KUPReportGenerator.Converters;
using KUPReportGenerator.Helpers.TaskProgress;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

internal class FilePdfReportGenerator : IReportGenerator
{
    private readonly IProgressContext _progressContext;
    private readonly IPdfConverter _pdfConverter;

    public FilePdfReportGenerator(IProgressContext progressContext, IPdfConverter pdfConverter)
    {
        _progressContext = progressContext;
        _pdfConverter = pdfConverter;
    }

    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var savePdfReportTask = _progressContext.AddTask("[green]Saving pdf report in a file.[/]");
        savePdfReportTask.Increment(50.0);
        var pdfResult = await _pdfConverter.HtmlToPdfAsync(Constants.HtmlReportFilePath, Constants.PdfReportFilePath,
            cancellationToken);
        savePdfReportTask.Increment(50.0);
        return pdfResult;
    }
}