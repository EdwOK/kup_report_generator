using FluentResults;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

public class ReportGeneratorPipeline : IReportGenerator
{
    private readonly IEnumerable<IReportGenerator> _reportGenerators;

    public ReportGeneratorPipeline(IEnumerable<IReportGenerator> reportGenerators) =>
        _reportGenerators = reportGenerators;

    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var reportResult = Result.Ok();

        foreach (var reportGenerator in _reportGenerators)
        {
            var generatorResult = await reportGenerator.Generate(reportContext, cancellationToken);

            reportResult = Result.Merge(reportResult, generatorResult);
        }

        return reportResult;
    }
}