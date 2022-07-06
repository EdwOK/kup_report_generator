using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

public interface IReportGenerator
{
    Task<Result> Generate(ReportSettings reportSettings, ProgressContext progressContext, CancellationToken cancellationToken = default);
}