using FluentValidation;

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

        RuleFor(s => s.ProjectAdoOrganizationName)
            .NotEmpty();

        RuleFor(s => s.RapidApiKey)
            .NotEmpty()
            .When(s => s.RapidApiKey != null);
    }
}