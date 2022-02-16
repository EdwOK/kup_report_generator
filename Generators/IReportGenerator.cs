using System.Threading;
using System.Threading.Tasks;
using FluentResults;

namespace KUPReportGenerator.Generators;

public interface IReportGenerator
{
    Task<Result> Generate(ReportSettings reportSettings, CancellationToken cancellationToken = default);
}