using FluentValidation;
using KUPReportGenerator.GitCommitsHistory.DataProviders;

namespace KUPReportGenerator.Report;

internal class ReportSettingsValidator : AbstractValidator<ReportSettings>
{
    public ReportSettingsValidator()
    {
        RuleFor(s => s.EmployeeFullName)
            .NotEmpty();

        RuleFor(s => s.EmployeeEmail)
            .EmailAddress();

        RuleFor(s => s.EmployeeJobPosition)
            .NotEmpty();

        RuleFor(s => s.EmployeeFolderName)
            .NotEmpty();

        RuleFor(s => s.ControlerFullName)
            .NotEmpty();

        RuleFor(s => s.ControlerJobPosition)
            .NotEmpty();

        RuleFor(s => s.ProjectName)
            .NotEmpty();

        RuleFor(s => s.GitCommitHistoryProvider)
            .NotNull()
            .WithMessage("Please reinstall the tool and select a commit history provider.");

        RuleFor(s => s.ProjectAdoOrganizationName)
            .NotEmpty()
            .When(s => s.GitCommitHistoryProvider == GitCommitsHistoryProvider.AzureDevOps)
            .WithMessage(
                "Please reinstall the tool and set the organization name for the Azure DevOps commit history provider.");

        RuleFor(s => s.ProjectGitDirectory)
            .NotEmpty()
            .When(s => s.GitCommitHistoryProvider == GitCommitsHistoryProvider.Local)
            .WithMessage(
                "Please reinstall the tool and configure the organization name for the local commit history provider.");

        RuleFor(s => s.RapidApiKey)
            .NotEmpty()
            .When(s => s.RapidApiKey is not null);
    }
}