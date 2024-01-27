using Helpers.TaskProgress;

namespace KUPReportGenerator.GitCommitsHistory.DataProviders;

public interface IGitCommitHistoryProviderFactory
{
    IGitCommitHistoryProvider Create(GitCommitsHistoryProvider provider, IProgressContext progressContext);
}

internal class GitCommitsHistoryProviderFactory : IGitCommitHistoryProviderFactory
{
    public IGitCommitHistoryProvider Create(GitCommitsHistoryProvider provider, IProgressContext progressContext) =>
        provider switch
        {
            GitCommitsHistoryProvider.AzureDevOps => new AdoGitCommitsHistoryProvider(progressContext),
            GitCommitsHistoryProvider.Local => new LocalGitCommitHistoryProvider(progressContext),
            _ => throw new ArgumentException("Invalid commit history provider!")
        };
}
