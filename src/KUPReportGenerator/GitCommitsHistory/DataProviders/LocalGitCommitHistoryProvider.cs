using System.Runtime.InteropServices;
using System.Text;
using CliWrap;
using Helpers;
using Helpers.TaskProgress;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.GitCommitsHistory.DataProviders;

internal class LocalGitCommitHistoryProvider(IProgressContext progressContext) : IGitCommitHistoryProvider
{
    public GitCommitsHistoryProvider Provider => GitCommitsHistoryProvider.Local;

    public async Task<Result<IEnumerable<GitCommitHistory>>> GetCommitsHistory(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        var error = new Error("Failed to get commits from the local system.");

        if (!Directory.Exists(reportContext.ReportSettings.ProjectGitDirectory))
        {
            return error.CausedBy(
                $"Project directory '{reportContext.ReportSettings.ProjectGitDirectory}' doesn't exist. Please reinstall the tool.");
        }

        var commitsHistoryProgressTask = progressContext.AddTask("[green]Getting history of commits.[/]");
        commitsHistoryProgressTask.Increment(50.0);

        var fromDate = DatetimeHelper.GetFirstDateOfMonth(reportContext.WorkingMonth);
        var toDate = DatetimeHelper.GetLastDateOfMonth(reportContext.WorkingMonth);
        var authors = $"({string.Join("|", reportContext.ReportSettings.EmployeeFullName)})";

        var commitsByRepositoriesTasks = Directory.GetDirectories(reportContext.ReportSettings.ProjectGitDirectory)
            .Select(r => (repositoryPath: r, commitsHistory: GetCommitsHistoryByRepository(r, authors, fromDate, toDate, cancellationToken)))
            .ToList();
        await Task.WhenAll(commitsByRepositoriesTasks.Select(r => r.commitsHistory));

        commitsHistoryProgressTask.Increment(50.0);

        var commitsHistory = new List<GitCommitHistory>();
        foreach ((string repositoryPath, Task<Result<string[]>> commitsLine) in commitsByRepositoriesTasks)
        {
            if (commitsLine.IsFaulted || commitsLine.Result.IsFailed)
            {
                continue;
            }

            var commits = commitsLine.Result.Value
                .Select(s =>
                {
                    var parts = s.Split(',');
                    return new GitCommit
                    {
                        CommitId = parts[0].TrimStart(),
                        Comment = parts[3].TrimStart(),
                        Author = new GitUserDate
                        {
                            Name = parts[1].TrimStart(),
                            Email = reportContext.ReportSettings.EmployeeEmail,
                            Date = DateTime.Parse(parts[2].Trim())
                        }
                    };
                })
                .ToArray();

            if (commits.Length == 0)
            {
                continue;
            }

            commitsHistory.Add(new GitCommitHistory
            {
                Repository = repositoryPath.Split(Path.DirectorySeparatorChar).Last(),
                Commits = commits
            });
        }

        return commitsHistory;
    }

    private static async Task<Result<string[]>> GetCommitsHistoryByRepository(string repository, string authors,
        DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var arguments =
            @$"-C ""{repository}"" log  --since=""{from:yyyy-MM-dd}"" --until=""{to:yyyy-MM-dd}"" --date=""short"" --pretty=format:""%h, %an, %ad, %s"" --author=""{authors}"" --no-merges --perl-regexp";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            arguments =
                @$"-C ""{repository}"" log  --since=""{from:yyyy-MM-dd}"" --until=""{to:yyyy-MM-dd}"" --date=""short"" --pretty=format:'%h, %an, %ad, %s' --author=""{authors}"" --no-merges --perl-regexp";
        }

        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();

        var commandResult = await Result.Try<CommandResult>(() =>
            Cli.Wrap("git")
                .WithArguments(arguments)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .ExecuteAsync(cancellationToken));

        if (commandResult.IsFailed)
        {
            return commandResult.ToResult();
        }

        var scriptErrors = SplitIntoLines(stdErrBuffer.ToString());
        var scriptOutput = SplitIntoLines(stdOutBuffer.ToString());

        return Result.Ok(scriptOutput).WithErrors(scriptErrors);

        static string[] SplitIntoLines(string str) =>
            str.Trim().Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }
}