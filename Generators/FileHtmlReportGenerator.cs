using FluentResults;
using HandlebarsDotNet;
using KUPReportGenerator.Helpers;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

internal class FileHtmlReportGenerator : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, ProgressContext progressContext,
        CancellationToken cancellationToken)
    {
        var generateHtmlReportTask = progressContext.AddTask("[green]Generating html report.[/]");
        generateHtmlReportTask.Increment(50.0);
        var htmlReport = await GenerateHtmlReport(reportContext, cancellationToken);
        generateHtmlReportTask.Increment(50.0);
        if (htmlReport.IsFailed)
        {
            return htmlReport.ToResult();
        }

        var saveHtmlReportTask = progressContext.AddTask("[green]Saving html report in a file.[/]");
        saveHtmlReportTask.Increment(50.0);
        var saveResult = await FileHelper.SaveAsync(Constants.HtmlReportFilePath, htmlReport.Value, cancellationToken);
        saveHtmlReportTask.Increment(50.0);
        if (saveResult.IsFailed)
        {
            return saveResult;
        }

        return saveResult;
    }

    private static async Task<Result<string>> GenerateHtmlReport(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var htmlReportTemplate = await FileHelper.ReadAsync(Constants.ReportTemplateFilePath, cancellationToken);
            if (htmlReportTemplate.IsFailed)
            {
                return htmlReportTemplate.ToResult();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var reportTemplate = Handlebars.Compile(htmlReportTemplate.Value);

            var currentDate = DateTime.UtcNow;
            var htmlReport = reportTemplate(new
            {
                month_name = DatetimeHelper.GetCurrentMonthName(),
                working_days = reportContext.WorkingDays,
                absences_days = reportContext.AbsencesDays,
                project_name = reportContext.ReportSettings.ProjectName,
                employee_fullname = reportContext.ReportSettings.EmployeeFullName,
                employee_job_position = reportContext.ReportSettings.EmployeeJobPosition,
                employee_commits_path =
                    @$"{reportContext.ReportSettings.EmployeeFolderName}\{currentDate.Year}\{currentDate.Month:00}\{Constants.CommitHistoryFileName}",
                controler_fullname = reportContext.ReportSettings.ControlerFullName,
                controler_job_position = reportContext.ReportSettings.ControlerJobPosition,
            });

            return Result.Ok(htmlReport);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Failed to generate html report.").CausedBy(exc));
        }
    }
}