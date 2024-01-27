using System.Globalization;
using GitCredentialManager;
using Helpers;
using Helpers.TaskProgress;
using KUPReportGenerator.Report;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace KUPReportGenerator.GitCommitsHistory.DataProviders;

internal class AdoGitCommitsHistoryProvider(IProgressContext progressContext) : IGitCommitHistoryProvider
{
    public GitCommitsHistoryProvider Provider => GitCommitsHistoryProvider.AzureDevOps;

    public async Task<Result<IEnumerable<GitCommitHistory>>> GetCommitsHistory(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        var error = new Error("Failed to get commits from AzureDevOps.");

        if (string.IsNullOrEmpty(reportContext.ReportSettings.ProjectAdoOrganizationName))
        {
            return error.CausedBy("An organization in settings hasn't been set. Please reinstall the tool.");
        }

        var credentialTask = progressContext.AddTask("[green]Getting git credentials.[/]");
        credentialTask.Increment(50.0);
        var credentials = FindCredentials(reportContext.ReportSettings.EmployeeEmail,
            reportContext.ReportSettings.ProjectAdoOrganizationName);
        credentialTask.Increment(50.0);
        if (credentials.IsFailed)
        {
            return error.CausedBy(credentials.Errors);
        }

        var connectionTask = progressContext.AddTask("[green]Connecting to the Azure DevOps Git API.[/]");
        connectionTask.Increment(50.0);
        var gitClientResult = CreateGitClient(credentials.Value, reportContext.ReportSettings.ProjectAdoOrganizationName,
            cancellationToken);
        connectionTask.Increment(50.0);
        if (gitClientResult.IsFailed)
        {
            return error.CausedBy(gitClientResult.Errors);
        }

        using GitHttpClient? gitClient = gitClientResult.Value;

        var fromDate = DatetimeHelper.GetFirstDateOfMonth(reportContext.WorkingMonth).ToString(CultureInfo.InvariantCulture);
        var toDate = DatetimeHelper.GetLastDateOfMonth(reportContext.WorkingMonth).ToString(CultureInfo.InvariantCulture);
        var commitsHistoryDict = new Dictionary<string, IEnumerable<GitCommitRef>>();

        var repositories = await Result.Try(() => gitClient.GetRepositoriesAsync(cancellationToken: cancellationToken));
        if (repositories.IsFailed)
        {
            return error.CausedBy(repositories.Errors);
        }

        var commitsHistoryProgressTask = progressContext.AddTask("[green]Getting history of commits.[/]",
            maxValue: repositories.Value.Count);
        var commitsFetchingProcessTasks =
            repositories.Value.Select(r => ProcessGetCommitsAsync(gitClient, r)).ToList();
        while (commitsFetchingProcessTasks.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            commitsHistoryProgressTask.Increment(1.0);

            var finishedCommitsHistoryTask = await Task.WhenAny(commitsFetchingProcessTasks).WaitAsync(cancellationToken);
            commitsFetchingProcessTasks.Remove(finishedCommitsHistoryTask);

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
        foreach ((string repository, IEnumerable<GitCommitRef> commits) in commitsHistoryDict)
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

        return commitsHistory;

        async Task<(GitRepository, Result<List<GitCommitRef>>)> ProcessGetCommitsAsync(GitHttpClient client, GitRepository repository)
        {
            var commitsQuery = new GitQueryCommitsCriteria
            {
                ItemVersion = new GitVersionDescriptor
                {
                    VersionType = GitVersionType.Branch,
                    VersionOptions = GitVersionOptions.None,
                    Version = repository.DefaultBranch?.Split('/').LastOrDefault("main")
                },
                Author = credentials.Value.Account,
                FromDate = fromDate,
                ToDate = toDate
            };

            var commits = await Result.Try(() => client.GetCommitsAsync(repository.Id, commitsQuery,
                cancellationToken: cancellationToken));

            return (repository, commits);
        }
    }

    private static Result<GitHttpClient> CreateGitClient(ICredential credentials, string organization,
        CancellationToken cancellationToken)
    {
        var vssCredentials = new VssBasicCredential(credentials.Account, credentials.Password);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/{organization}"), vssCredentials);
        return Result.Try(() => connection.GetClient<GitHttpClient>(cancellationToken));
    }

    private static Result<ICredential> FindCredentials(string email, string organization)
    {
        var error = new Error($"Failed to find git credentials for {email} and {organization}.");

        var services = new[]
        {
            $"{organization}@azure.devops",
            $"git:https://{organization}@dev.azure.com/{organization}",
            $"git:https://dev.azure.com/{organization}"
        };

        var store = Result.Try(() => CredentialManager.Create(nameof(KUPReportGenerator)));
        if (store.IsFailed)
        {
            return error.CausedBy(store.Errors);
        }

        foreach (var service in services)
        {
            var credentials = Result.Try(() => store.Value.Get(service, email));
            if (credentials.IsFailed || credentials.ValueOrDefault is null)
            {
                continue;
            }

            return Result.Ok(credentials.Value);
        }

        return error;
    }
}