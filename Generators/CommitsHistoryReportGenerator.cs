using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using KUPReportGenerator.Helpers;

namespace KUPReportGenerator
{
    internal class CommitsHistoryReportGenerator : IReportGenerator
    {
        public async Task<Result> Generate(ReportSettings reportSettings, CancellationToken cancellationToken)
        {
            var commitsHistory = await GetCommitsHistory(reportSettings, cancellationToken);
            if (commitsHistory.ValueOrDefault.Length == 0)
            {
                return commitsHistory.ToResult();
            }
            
            return await FileHelper.SaveAsync(Constants.CommitsHistoryFilePath, commitsHistory.Value, cancellationToken);
        }

        private static async Task<Result<string>> GetCommitsHistory(ReportSettings reportSettings, 
            CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(reportSettings.ProjectRootFolder))
                {
                    return Result.Fail($"{reportSettings.ProjectRootFolder} doesn't exist in the file system.");
                }

                var delimiterLine = new string('-', 72);
                var results = new Result<string>();

                cancellationToken.ThrowIfCancellationRequested();
                var commitsByRepositoriesTasks = Directory.GetDirectories(reportSettings.ProjectRootFolder)
                    .Select(r => (repositoryPath: r, commitsHistory: GetCommitsHistoryByRepository(r, reportSettings, cancellationToken)))
                    .ToList();

                await Task.WhenAll(commitsByRepositoriesTasks.Select(r => r.commitsHistory));

                foreach (var (repositoryPath, commitsHistory) in commitsByRepositoriesTasks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (commitsHistory.Result.IsSuccess)
                    {
                        var commitsHistoryRepoBuilder = new StringBuilder();

                        var repositoryName = GetRepositoryName(repositoryPath);
                        commitsHistoryRepoBuilder.AppendLine($"PROJECT: {repositoryName}, branch: main:");
                        commitsHistoryRepoBuilder.AppendLine();

                        if (commitsHistory.Result.Value.LongLength > 0)
                        {
                            commitsHistoryRepoBuilder.AppendJoin(Environment.NewLine, commitsHistory.Result.Value);
                        }

                        commitsHistoryRepoBuilder.AppendLine();
                        commitsHistoryRepoBuilder.AppendLine(delimiterLine);
                        commitsHistoryRepoBuilder.AppendLine();

                        results = results.WithValue(results.Value + commitsHistoryRepoBuilder);
                    }
                    else
                    {
                        results = Result.Merge(results, commitsHistory.Result);
                    }
                }

                return results;
            }
            catch (Exception exc)
            {
                return Result.Fail(new Error("Failed with generation history of commits.").CausedBy(exc));
            }

            static string GetRepositoryName(string repositoryPath) => 
                repositoryPath.Split(Path.DirectorySeparatorChar)[^1];
        }

        private static async Task<Result<string[]>> GetCommitsHistoryByRepository(string repository, ReportSettings reportSettings,
            CancellationToken cancellationToken)
        {
            try
            {
                var authors = $"({string.Join("|", reportSettings.EmployeeCommitsAuthors)})";

                string scriptCommand = CommandLineHelper.IsWindowsPlatform()
                    ? @$"git -C ""{repository}"" log --since=""last month"" --date=""short"" --pretty=format:""%h, %an, %ad, %s"" --author=""{authors}"" --no-merges --perl-regexp"
                    : @$"git -C ""{repository}"" log --since=""last month"" --date=""short"" --pretty=format:'%h, %an, %ad, %s' --author=""{authors}"" --no-merges --perl-regexp";

                var scriptProcess = await CommandLineHelper.RunCommandAsync(scriptCommand, cancellationToken: cancellationToken);
                if (scriptProcess.IsFailed)
                {
                    return scriptProcess.ToResult();
                }

                var standardError = scriptProcess.Value.StandardError.ReadToEndAsync();
                var standardOutput = scriptProcess.Value.StandardOutput.ReadToEndAsync();

                var scriptErrors = SplitIntoLines(await standardError.WithCancellation(cancellationToken));
                var scriptOutput = SplitIntoLines(await standardOutput.WithCancellation(cancellationToken));

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
}