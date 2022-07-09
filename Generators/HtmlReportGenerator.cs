using System.Globalization;
using FluentResults;
using HandlebarsDotNet;
using KUPReportGenerator.Helpers;
using Spectre.Console;

namespace KUPReportGenerator.Generators;

internal class HtmlReportGenerator : IReportGenerator
{
    public async Task<Result> Generate(ReportSettings reportSettings, ProgressContext progressContext, CancellationToken cancellationToken)
    {
        var generateHtmlReportTask = progressContext.AddTask("[green]Generate HTML KUP report.[/]");
        generateHtmlReportTask.Increment(50.0);
        var htmlReport = await GenerateHtmlReport(reportSettings, cancellationToken);
        generateHtmlReportTask.Increment(50.0);
        if (htmlReport.IsFailed)
        {
            return htmlReport.ToResult();
        }

        var saveReportTask = progressContext.AddTask("[green]Saving KUP in a report file.[/]");
        saveReportTask.Increment(50.0);
        var saveResult = await FileHelper.SaveAsync(Constants.ReportFilePath, htmlReport.Value, cancellationToken);
        saveReportTask.Increment(50.0);
        return saveResult;
    }

    private static async Task<Result<string>> GenerateHtmlReport(ReportSettings settings, CancellationToken cancellationToken)
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
                month_name = currentDate.ToString("MMMM", CultureInfo.InvariantCulture),
                working_days = settings.WorkingDays,
                absences_days = settings.AbsencesDays,
                project_name = settings.ProjectName,
                employee_fullname = settings.EmployeeFullName,
                employee_position = settings.EmployeePosition,
                employee_commits_path =
                    @$"{settings.EmployeeFolderName}\{currentDate.Year}\{currentDate.Month:00}\Commits.txt",
                controler_fullname = settings.ControlerFullName,
                controler_position = settings.ControlerPosition,
            });

            return Result.Ok(htmlReport);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error($"Failed with generation report for {Constants.ReportTemplateFilePath}.")
                .CausedBy(exc));
        }
    }
}