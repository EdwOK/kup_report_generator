namespace KUPReportGenerator.GitCommitsHistory;

public record GitCommit
{
    /// <summary>
    /// ID (SHA-1) of the commit.
    /// </summary>
    public required string CommitId { get; init; }

    /// <summary>
    /// Comment or message of the commit.
    /// </summary>
    public required string Comment { get; init; }

    /// <summary>
    /// Author of the commit.
    /// </summary>
    public required GitUserDate Author { get; init; }
}