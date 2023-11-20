using KUPReportGenerator.Report;

namespace KUPReportGenerator.GitCommitsHistory;

public interface IGitCommitHistoryProvider
{
    Task<Result<IEnumerable<GitCommitHistory>>> GetCommitsHistory(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken);
}