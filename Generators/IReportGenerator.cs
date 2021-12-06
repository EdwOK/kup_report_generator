using FluentResults;
using System.Threading;
using System.Threading.Tasks;

namespace KUPReportGenerator
{
    public interface IReportGenerator
    {
        Task<Result> Generate(ReportSettings reportSettings, CancellationToken cancellationToken = default);
    }
}
