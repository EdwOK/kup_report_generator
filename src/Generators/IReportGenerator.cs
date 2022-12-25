using FluentResults;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

public interface IReportGenerator
{
    Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken);
}