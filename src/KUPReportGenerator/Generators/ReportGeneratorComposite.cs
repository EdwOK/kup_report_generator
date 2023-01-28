using FluentResults;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

public class ReportGeneratorComposite : IReportGenerator
{
    private readonly IEnumerable<IReportGenerator> _reportGenerators;

    public ReportGeneratorComposite(IEnumerable<IReportGenerator> reportGenerators) =>
        _reportGenerators = reportGenerators;

    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        foreach (var reportGenerator in _reportGenerators)
        {
            var reportResult = await reportGenerator.Generate(reportContext, cancellationToken);
            if (reportResult.IsFailed)
            {
                return reportResult;
            }
        }

        return Result.Ok();
    }
}