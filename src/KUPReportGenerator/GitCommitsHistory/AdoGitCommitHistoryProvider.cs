using System.Globalization;
using FluentResults;
using GitCredentialManager;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Report;
using KUPReportGenerator.TaskProgress;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using CredentialManager = KUPReportGenerator.Helpers.CredentialManager;

namespace KUPReportGenerator.GitCommitsHistory;

internal class AdoGitCommitHistoryProvider : IGitCommitHistoryProvider
{
    private readonly IProgressContext _progressContext;

    public AdoGitCommitHistoryProvider(IProgressContext progressContext) =>
        _progressContext = progressContext;

    public async Task<Result<IEnumerable<GitCommitHistory>>> GetCommitsHistory(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var credentialTask = _progressContext.AddTask("[green]Getting git credentials.[/]");
            credentialTask.Increment(50.0);
            var credentials = FindCredentials(reportContext.ReportSettings.EmployeeEmail,
                reportContext.ReportSettings.ProjectAdoOrganizationName);
            credentialTask.Increment(50.0);
            if (credentials.IsFailed)
            {
                return credentials.ToResult();
            }

            var connectionTask = _progressContext.AddTask("[green]Connecting to the Azure DevOps Git API.[/]");
            connectionTask.Increment(50.0);
            var clientResult = TryConnectToAdo(credentials.Value, reportContext.ReportSettings.ProjectAdoOrganizationName,
                cancellationToken);
            connectionTask.Increment(50.0);
            if (clientResult.IsFailed)
            {
                return clientResult.ToResult();
            }

            using var client = clientResult.Value;

            var repositories = await Result.Try(() => client.GetRepositoriesAsync(cancellationToken: cancellationToken));
            if (repositories.IsFailed)
            {
                return repositories.ToResult();
            }

            var fromDate = DatetimeHelper.GetFirstDateOfCurrentMonth().ToString(CultureInfo.InvariantCulture);
            var toDate = DatetimeHelper.GetLastDateOfCurrentMonth().ToString(CultureInfo.InvariantCulture);
            var commitsHistoryDict = new Dictionary<string, IEnumerable<GitCommitRef>>();

            var commitsHistoryProgressTask = _progressContext.AddTask("[green]Getting history of commits.[/]",
                maxValue: repositories.Value.Count);

            var commitsHistoryTasks = repositories.Value.Select(ProcessGetCommitsAsync).ToList();
            while (commitsHistoryTasks.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                commitsHistoryProgressTask.Increment(1.0);

                var finishedCommitsHistoryTask = await Task.WhenAny(commitsHistoryTasks).WaitAsync(cancellationToken);
                commitsHistoryTasks.Remove(finishedCommitsHistoryTask);

                (GitRepository? repository, Result<List<GitCommitRef>>? commits) = finishedCommitsHistoryTask.Result;
                if (commits.IsFailed)
                {
                    continue;
                }

                if (commits.Value.Count > 0)
                {
                    commitsHistoryDict.Add(repository.Name, commits.Value);
                }
            }

            var commitsHistory = new List<GitCommitHistory>();
            foreach (var (repository, commits) in commitsHistoryDict)
            {
                commitsHistory.Add(new GitCommitHistory
                {
                    Repository = repository,
                    Commits = commits.Select(v => new GitCommit
                    {
                        CommitId = v.CommitId,
                        Comment = v.Comment,
                        Author = new GitUserDate
                        {
                            Name = v.Author.Name,
                            Email = v.Author.Email,
                            Date = v.Author.Date,
                            ImageUrl = v.Author.ImageUrl,
                        },
                    })
                });
            }

            return Result.Ok<IEnumerable<GitCommitHistory>>(commitsHistory);

            async Task<(GitRepository, Result<List<GitCommitRef>>)> ProcessGetCommitsAsync(GitRepository repository)
            {
                var repoCommitsHistory = await Result.Try(() =>
                    client.GetCommitsAsync(repository.Id,
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
            var connection = new VssConnection(new Uri($"https://dev.azure.com/{organization}"), vssCredentials);
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

        return Result.Fail($"Failed to find git credentials for {email} and {organization}.");
    }
}