﻿using System.Text;
using KUPReportGenerator.GitCommitsHistory;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Helpers.TaskProgress;
using KUPReportGenerator.Report;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

internal class CommitsHistoryReportGenerator(
    IProgressContext progressContext,
    IGitCommitHistoryProvider gitCommitHistoryProvider) : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var commitsHistory = await BuildCommitsHistory(reportContext, cancellationToken);

        var saveCommitsHistoryTask = progressContext.AddTask("[green]Saving commits history in the report file.[/]");
        saveCommitsHistoryTask.Increment(50.0);
        await FileHelper.SaveAsync(Constants.CommitsHistoryFilePath, Encoding.UTF8.GetBytes(commitsHistory.ValueOrDefault ?? ""),
            cancellationToken);
        saveCommitsHistoryTask.Increment(50.0);

        return commitsHistory.ToResult();
    }

    private async Task<Result<string>> BuildCommitsHistory(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        try
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

            return Result.Ok(resultBuilder.ToString());
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Failed to generate commits history.").CausedBy(exc));
        }
    }
}