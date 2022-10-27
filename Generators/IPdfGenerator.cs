using FluentResults;

namespace KUPReportGenerator.Generators;

internal interface IPdfGenerator
{
    Task<Result> HtmlToPdfAsync(string htmlPath, string pdfPath, CancellationToken cancellationToken = default);
}
