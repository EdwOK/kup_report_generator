using System.Text;
using Helpers;
using Helpers.TaskProgress;
using KUPReportGenerator.GitCommitsHistory.DataProviders;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

internal class CommitsHistoryReportGenerator(
    IProgressContext progressContext,
    IGitCommitHistoryProvider gitCommitHistoryProvider) : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var error = new Error("Failed to generate commits history.");

        var commitsHistory = await Result.Try(() => BuildCommitsHistory(reportContext, cancellationToken),
            error.CausedBy);
        if (commitsHistory.Value.IsFailed)
        {
            return commitsHistory.Value.ToResult();
        }

        var saveCommitsHistoryTask = progressContext.AddTask("[green]Saving commits history in the report file.[/]");
        saveCommitsHistoryTask.Increment(50.0);
        var saveCommits = await Result.Try(() => SaveCommits(commitsHistory.Value.ValueOrDefault, cancellationToken),
            error.CausedBy);
        saveCommitsHistoryTask.Increment(50.0);

        return saveCommits;
    }

    private static async Task SaveCommits(string? fileContent, CancellationToken cancellationToken)
    {
        var reportContent = Encoding.UTF8.GetBytes(fileContent ?? "");
        await FileHelper.SaveAsync(Constants.CommitsHistoryFilePath, reportContent, cancellationToken);
    }

    private async Task<Result<string>> BuildCommitsHistory(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var commitsHistory = await gitCommitHistoryProvider.GetCommitsHistory(reportContext, cancellationToken);
        if (commitsHistory.IsFailed)
        {
            return commitsHistory.ToResult();
        }

        var resultBuilder = new StringBuilder();
        var delimiterLine = new string('-', 72);

        foreach (var history in commitsHistory.Value)
        {
            var commitHistoryBuilder = new StringBuilder();

            commitHistoryBuilder.AppendLine($"PROJECT: {history.Repository}:");
            commitHistoryBuilder.AppendLine();
            commitHistoryBuilder.AppendJoin(Environment.NewLine,
                history.Commits.Select(c =>
                    $"{c.CommitId[..7]}, {c.Author.Name}, {c.Author.Date:yyyy-MM-dd}, {c.Comment}"));
            commitHistoryBuilder.AppendLine();
            commitHistoryBuilder.AppendLine(delimiterLine);
            commitHistoryBuilder.AppendLine();

            resultBuilder.Append(commitHistoryBuilder);
        }

        return resultBuilder.ToString();
    }
}