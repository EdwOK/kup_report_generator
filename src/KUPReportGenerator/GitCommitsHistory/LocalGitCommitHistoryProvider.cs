using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using CliWrap;
using FluentResults;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Helpers.TaskProgress;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.GitCommitsHistory;

internal class LocalGitCommitHistoryProvider : IGitCommitHistoryProvider
{
    private readonly IProgressContext _progressContext;

    public LocalGitCommitHistoryProvider(IProgressContext progressContext) =>
        _progressContext = progressContext;

    public async Task<Result<IEnumerable<GitCommitHistory>>> GetCommitsHistory(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(reportContext.ReportSettings.ProjectGitDirectory))
        {
            return Result.Fail($"Project directory: '{reportContext.ReportSettings.ProjectGitDirectory}' doesn't exist in the file system.");
        }

        try
        {
            var commitsHistoryProgressTask = _progressContext.AddTask("[green]Getting history of commits.[/]");
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
            foreach (var (repositoryPath, commitsLine) in commitsByRepositoriesTasks)
            {
                var commits = commitsLine.Result.ValueOrDefault.Select(s =>
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
                }).ToArray();

                if (commits?.Length == 0)
                {
                    continue;
                }

                commitsHistory.Add(new GitCommitHistory
                {
                    Repository = repositoryPath.Split(Path.DirectorySeparatorChar).Last()!,
                    Commits = commits!
                });
            }

            return commitsHistory;
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Failed to get commits history.").CausedBy(exc));
        }
    }

    private static async Task<Result<string[]>> GetCommitsHistoryByRepository(string repository, string authors,
        DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        try
        {
            string arguments =
                @$"-C ""{repository}"" log  --since=""{from:yyyy-MM-dd}"" --until=""{to:yyyy-MM-dd}"" --date=""short"" --pretty=format:""%h, %an, %ad, %s"" --author=""{authors}"" --no-merges --perl-regexp";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments =
                    @$"-C ""{repository}"" log  --since=""{from:yyyy-MM-dd}"" --until=""{to:yyyy-MM-dd}"" --date=""short"" --pretty=format:'%h, %an, %ad, %s' --author=""{authors}"" --no-merges --perl-regexp";
            }

            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();

            var commandResult = await Cli.Wrap("git")
                .WithArguments(arguments)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .ExecuteAsync(cancellationToken);

            var scriptErrors = SplitIntoLines(stdErrBuffer.ToString());
            var scriptOutput = SplitIntoLines(stdOutBuffer.ToString());

            return Result.Ok(scriptOutput).WithErrors(scriptErrors);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed with getting history of commits for {repository}.").CausedBy(exc));
        }

        static string[] SplitIntoLines(string str) =>
            str.Trim().Split("\n", StringSplitOptions.RemoveEmptyEntries);
    }
}