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
    public async Task<Result> Generate(ReportSettings reportSettings, ProgressContext progressContext, CancellationToken cancellationToken)
    {
        var commitsHistory = await GetCommitsHistory(reportSettings, progressContext, cancellationToken);
        if (commitsHistory.IsFailed)
        {
            return commitsHistory.ToResult();
        }

        if (commitsHistory.ValueOrDefault.Length == 0)
        {
            return Result.Fail("Sorry, you don't have commit history.");
        }

        var saveCommitsHistoryTask = progressContext.AddTask("[green]Saving commits history in a report file.[/]");
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
                    commitsHistory.Select(c => $"{c.CommitId[..7]}, {c.Author.Name}, {c.Author.Date:yyyy-MM-dd}, {c.Comment}"));
                commitHistoryBuilder.AppendLine();
                commitHistoryBuilder.AppendLine(delimiterLine);
                commitHistoryBuilder.AppendLine();

                resultBuilder.Append(commitHistoryBuilder);
            }

            return Result.Ok(resultBuilder.ToString());
        }
        catch (Exception exc)

        {
            return Result.Fail(new Error("Failed with generation history of commits.").CausedBy(exc));
        }
    }

    private static async Task<Result<Dictionary<string, IEnumerable<GitCommitRef>>>> GetAdoCommitsHistory(ReportSettings reportSettings,
        ProgressContext progressContext, CancellationToken cancellationToken)
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

            var connectionTask = progressContext.AddTask("[green]Connecting to the Azure DevOps Git API services.[/]");
            connectionTask.Increment(50.0);
            var client = TryConnectToAdo(credentials.Value, reportSettings.ProjectAdoOrganizationName, cancellationToken);
            connectionTask.Increment(50.0);
            if (client.IsFailed)
            {
                return client.ToResult();
            }

            var fromDate = DatetimeHelper.GetFirstDateOfMonth().ToString(CultureInfo.InvariantCulture);
            var toDate = DatetimeHelper.GetLastDateOfMonth().ToString(CultureInfo.InvariantCulture);

            var allCommitsHistory = new Dictionary<string, IEnumerable<GitCommitRef>>();
            var commitsHistoryErrors = new List<IError>();

            var repositories = await client.Value.GetRepositoriesAsync(cancellationToken: cancellationToken);
            var traversingCommitsTask = progressContext.AddTask("[green]Getting history of commits.[/]", maxValue: repositories.Count);

            foreach (var repository in repositories)
            {
                traversingCommitsTask.Increment(1.0);

                var repoCommitsHistory = await Result.Try(() => client.Value.GetCommitsAsync(repository.Id,
                    new GitQueryCommitsCriteria
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
                    },
                    cancellationToken: cancellationToken));
                if (repoCommitsHistory.IsFailed)
                {
                    commitsHistoryErrors.AddRange(repoCommitsHistory.Errors);
                    continue;
                }

                if (repoCommitsHistory.Value.Count > 0)
                {
                    allCommitsHistory.Add(repository.Name, repoCommitsHistory.Value);
                }
            }

            if (allCommitsHistory.Count == 0 && commitsHistoryErrors.Count > 0)
            {
                return Result.Fail($"Failed with getting history of commits.").WithErrors(commitsHistoryErrors);
            }

            return Result.Ok(allCommitsHistory);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed with getting history of commits.").CausedBy(exc));
        }
    }

    private static Result<GitHttpClient> TryConnectToAdo(ICredential credentials, string employeeOrganization, CancellationToken cancellationToken)
    {
        try
        {
            var vssCredentials = new VssBasicCredential(credentials.Account, credentials.Password);
            var connection = new VssConnection(new Uri($"https://dev.azure.com/{employeeOrganization}"), vssCredentials);
            var client = connection.GetClient<GitHttpClient>(cancellationToken);
            return Result.Ok(client);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed with connection to the Azure DevOps API service.").CausedBy(exc));
        }
    }

    private static Result<ICredential> FindCredentials(string employeeEmail, string employeeOrganization)
    {
        var store = CredentialManager.Create(nameof(KUPReportGenerator));
        if (store.IsFailed)
        {
            return store.ToResult();
        }

        var services = new[]
        {
            $"{employeeOrganization}@azure.devops",
            $"git:https://{employeeOrganization}@dev.azure.com/{employeeOrganization}",
            $"git:https://dev.azure.com/{employeeOrganization}"
        };

        foreach (var service in services)
        {
            var credentials = Result.Try(() => store.Value.Get(service, employeeEmail));
            if (credentials.IsFailed || credentials.ValueOrDefault is null)
            {
                continue;
            }

            return credentials;
        }

        return Result.Fail(new Error("Could not find git credentials.")
            .WithMetadata("EmployeeEmail", employeeEmail)
            .WithMetadata("EmployeeOrganization", employeeOrganization));
    }
}