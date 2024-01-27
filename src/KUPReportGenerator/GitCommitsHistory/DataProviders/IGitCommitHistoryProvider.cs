using KUPReportGenerator.Report;

namespace KUPReportGenerator.GitCommitsHistory.DataProviders;

public interface IGitCommitHistoryProvider
{
    GitCommitsHistoryProvider Provider { get; }

    Task<Result<IEnumerable<GitCommitHistory>>> GetCommitsHistory(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken);
}
