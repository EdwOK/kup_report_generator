namespace KUPReportGenerator.GitCommitsHistory;

public record GitCommitHistory
{
    /// <summary>
    /// The name of the repository that the commit belongs to.
    /// </summary>
    public required string Repository { get; init; }

    /// <summary>
    /// List of commits.
    /// </summary>
    public required IEnumerable<GitCommit> Commits { get; init; }
}