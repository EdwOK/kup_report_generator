using FluentResults;

namespace KUPReportGenerator.Converters;

internal interface IPdfConverter
{
    Task<Result> HtmlToPdfAsync(string htmlPath, string pdfPath, CancellationToken cancellationToken = default);
}