using FluentResults;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

public class ReportGeneratorComposite : IReportGenerator
{
    private readonly IEnumerable<IReportGenerator> _reportGenerators;

    public ReportGeneratorComposite(IEnumerable<IReportGenerator> reportGenerators) =>
        _reportGenerators = reportGenerators;

    public async Task<Result> Generate(ReportGeneratorContext reportContext, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        foreach (var reportGenerator in _reportGenerators)
        {
            var reportResult = await reportGenerator.Generate(reportContext, progressContext, cancellationToken);
            if (reportResult.IsFailed)
            {
                return reportResult;
            }
        }

        return Result.Ok();
    }
}