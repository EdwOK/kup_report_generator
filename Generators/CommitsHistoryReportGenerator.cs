using System.Globalization;
using System.Text;
using FluentResults;
using GitCredentialManager;
using KUPReportGenerator.Helpers;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

internal class CommitsHistoryReportGenerator : IReportGenerator
{
    public async Task<Result> Generate(ReportSettings reportSettings, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        var commitsHistory = await GetCommitsHistory(reportSettings, progressContext, cancellationToken);
        if (commitsHistory.IsFailed)
        {
            return commitsHistory.ToResult();
        }

        if (commitsHistory.ValueOrDefault.Length == 0)
        {
            return Result.Fail("Commits history is empty.");
        }

        var saveCommitsHistoryTask = progressContext.AddTask("[green]Saving commits history in the report file.[/]");
        saveCommitsHistoryTask.Increment(50.0);
        var saveResult = await FileHelper.SaveAsync(Constants.CommitsHistoryFilePath, commitsHistory.Value, cancellationToken);
        saveCommitsHistoryTask.Increment(50.0);
        return saveResult;
    }

    private static async Task<Result<string>> GetCommitsHistory(ReportSettings reportSettings, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var adoCommits = await GetAdoCommitsHistory(reportSettings, progressContext, cancellationToken);
            if (adoCommits.IsFailed)
            {
                return adoCommits.ToResult();
            }

            var resultBuilder = new StringBuilder();
            var delimiterLine = new string('-', 72);

            foreach (var (repository, commitsHistory) in adoCommits.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var commitHistoryBuilder = new StringBuilder();

                commitHistoryBuilder.AppendLine($"PROJECT: {repository}:");
                commitHistoryBuilder.AppendLine();
                commitHistoryBuilder.AppendJoin(Environment.NewLine,
                    commitsHistory.Select(c =>
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

    private static async Task<Result<Dictionary<string, IEnumerable<GitCommitRef>>>> GetAdoCommitsHistory(
        ReportSettings reportSettings, ProgressContext progressContext, CancellationToken cancellationToken)
    {
        try
        {
            var credentialTask = progressContext.AddTask("[green]Getting git credentials.[/]");
            credentialTask.Increment(50.0);
            var credentials = FindCredentials(reportSettings.EmployeeEmail, reportSettings.ProjectAdoOrganizationName);
            credentialTask.Increment(50.0);
            if (credentials.IsFailed)
            {
                return credentials.ToResult();
            }

            var connectionTask = progressContext.AddTask("[green]Connecting to the Azure DevOps Git API.[/]");
            connectionTask.Increment(50.0);
            var client = TryConnectToAdo(credentials.Value, reportSettings.ProjectAdoOrganizationName,
                cancellationToken);
            connectionTask.Increment(50.0);
            if (client.IsFailed)
            {
                return client.ToResult();
            }

            var repositories =
                await Result.Try(() => client.Value.GetRepositoriesAsync(cancellationToken: cancellationToken));
            if (repositories.IsFailed)
            {
                return repositories.ToResult();
            }

            var fromDate = DatetimeHelper.GetFirstDateOfCurrentMonth().ToString(CultureInfo.InvariantCulture);
            var toDate = DatetimeHelper.GetLastDateOfCurrentMonth().ToString(CultureInfo.InvariantCulture);
            var allCommitsHistory = new Dictionary<string, IEnumerable<GitCommitRef>>();
            var commitsHistoryErrors = new List<IError>();

            var commitsHistoryProgressTask = progressContext.AddTask("[green]Getting history of commits.[/]",
                maxValue: repositories.Value.Count);

            var commitsHistoryTasks = repositories.Value.Select(ProcessGetCommitsAsync).ToList();
            while (commitsHistoryTasks.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                commitsHistoryProgressTask.Increment(1.0);

                var finishedCommitsHistoryTask = await Task.WhenAny(commitsHistoryTasks).WaitAsync(cancellationToken);
                commitsHistoryTasks.Remove(finishedCommitsHistoryTask);

                (GitRepository? repository, Result<List<GitCommitRef>>? commitsHistory) = finishedCommitsHistoryTask.Result;
                if (commitsHistory.IsFailed)
                {
                    commitsHistoryErrors.AddRange(commitsHistory.Errors);
                    continue;
                }

                if (commitsHistory.Value.Count > 0)
                {
                    allCommitsHistory.Add(repository.Name, commitsHistory.Value);
                }
            }

            if (allCommitsHistory.Count == 0 && commitsHistoryErrors.Count > 0)
            {
                return Result.Fail("Failed to get commits history.").WithErrors(commitsHistoryErrors);
            }

            return Result.Ok(allCommitsHistory);

            async Task<(GitRepository, Result<List<GitCommitRef>>)> ProcessGetCommitsAsync(GitRepository repository)
            {
                var repoCommitsHistory = await Result.Try(() =>
                    client?.Value.GetCommitsAsync(repository.Id,
                        new GitQueryCommitsCriteria
                        {
                            ItemVersion = new GitVersionDescriptor
                            {
                                VersionType = GitVersionType.Branch,
                                VersionOptions = GitVersionOptions.None,
                                Version = repository.DefaultBranch?.Split('/').LastOrDefault("main")
                            },
                            Author = credentials?.Value.Account,
                            FromDate = fromDate,
                            ToDate = toDate
                        },
                        cancellationToken: cancellationToken));

                return (repository, repoCommitsHistory);
            }
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Failed to get commits history.").CausedBy(exc));
        }
    }

    private static Result<GitHttpClient> TryConnectToAdo(ICredential credentials, string organization,
        CancellationToken cancellationToken)
    {
        try
        {
            var vssCredentials = new VssBasicCredential(credentials.Account, credentials.Password);
            var connection =
                new VssConnection(new Uri($"https://dev.azure.com/{organization}"), vssCredentials);
            var client = connection.GetClient<GitHttpClient>(cancellationToken);
            return Result.Ok(client);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Failed to connect to the Azure DevOps API.").CausedBy(exc));
        }
    }

    private static Result<ICredential> FindCredentials(string email, string organization)
    {
        var store = CredentialManager.Create(nameof(KUPReportGenerator));
        if (store.IsFailed)
        {
            return store.ToResult();
        }

        var services = new[]
        {
            $"{organization}@azure.devops",
            $"git:https://{organization}@dev.azure.com/{organization}",
            $"git:https://dev.azure.com/{organization}"
        };

        foreach (var service in services)
        {
            var credentials = Result.Try(() => store.Value.Get(service, email));
            if (credentials.IsFailed || credentials.ValueOrDefault is null)
            {
                continue;
            }

            return credentials;
        }

        return Result.Fail(new Error($"Failed to find git credentials for {email} and {organization}."));
    }
}