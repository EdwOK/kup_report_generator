using System.Text;
using FluentResults;
using HandlebarsDotNet;
using KUPReportGenerator.Helpers;
using KUPReportGenerator.Helpers.TaskProgress;
using KUPReportGenerator.Properties;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

internal class FileHtmlReportGenerator : IReportGenerator
{
    private readonly IProgressContext _progressContext;

    public FileHtmlReportGenerator(IProgressContext progressContext) =>
        _progressContext = progressContext;

    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var generateHtmlReportTask = _progressContext.AddTask("[green]Generating html report.[/]");
        generateHtmlReportTask.Increment(50.0);
        var htmlReport = await GenerateHtmlReport(reportContext, cancellationToken);
        generateHtmlReportTask.Increment(50.0);
        if (htmlReport.IsFailed)
        {
            return htmlReport.ToResult();
        }

        var saveHtmlReportTask = _progressContext.AddTask("[green]Saving html report in a file.[/]");
        saveHtmlReportTask.Increment(50.0);
        await FileHelper.SaveAsync(Constants.HtmlReportFilePath, Encoding.UTF8.GetBytes(htmlReport.Value),
            cancellationToken);
        saveHtmlReportTask.Increment(50.0);

        return Result.Ok();
    }

    private static async Task<Result<string>> GenerateHtmlReport(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var reportTemplate = await Task.Run(() => Handlebars.Compile(Resources.report_template), cancellationToken);

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