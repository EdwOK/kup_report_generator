using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

public class ReportGeneratorPipeline(IEnumerable<IReportGenerator> reportGenerators) : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var reportResult = Result.Ok();

        foreach (var reportGenerator in reportGenerators)
        {
            var generatorResult = await reportGenerator.Generate(reportContext, cancellationToken);

            reportResult = Result.Merge(reportResult, generatorResult);
        }

        return reportResult;
    }
}