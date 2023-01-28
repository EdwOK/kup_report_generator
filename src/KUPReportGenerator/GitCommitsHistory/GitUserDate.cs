namespace KUPReportGenerator.GitCommitsHistory;

public record GitUserDate
{
    /// <summary>
    /// Name of the user performing the Git operation.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Email address of the user performing the Git operation.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Date of the Git operation.
    /// </summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// Url for the user's avatar.
    /// </summary>
    public string? ImageUrl { get; init; }
}