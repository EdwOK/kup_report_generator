namespace KUPReportGenerator.Converters;

public interface IPdfConverter
{
    Task<Result> HtmlToPdfAsync(string htmlPath, string pdfPath, CancellationToken cancellationToken = default);
}