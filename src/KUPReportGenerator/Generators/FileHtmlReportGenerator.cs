using System.Text;
using HandlebarsDotNet;
using Helpers;
using Helpers.TaskProgress;
using KUPReportGenerator.Properties;
using KUPReportGenerator.Report;

namespace KUPReportGenerator.Generators;

internal class FileHtmlReportGenerator(IProgressContext progressContext) : IReportGenerator
{
    public async Task<Result> Generate(ReportGeneratorContext reportContext, CancellationToken cancellationToken)
    {
        var error = new Error("Failed to generate html report.");

        var generateHtmlReportTask = progressContext.AddTask("[green]Generating html report.[/]");
        generateHtmlReportTask.Increment(50.0);
        var htmlReport = await Result.Try(() => GenerateHtmlReport(reportContext, cancellationToken),
            error.CausedBy);
        generateHtmlReportTask.Increment(50.0);
        if (htmlReport.Value.IsFailed)
        {
            return htmlReport.Value.ToResult();
        }

        var saveHtmlReportTask = progressContext.AddTask("[green]Saving html report in a file.[/]");
        saveHtmlReportTask.Increment(50.0);
        var saveReport = await Result.Try(() => SaveReport(htmlReport.ValueOrDefault.ValueOrDefault, cancellationToken),
            error.CausedBy);
        saveHtmlReportTask.Increment(50.0);

        return saveReport;
    }

    private static async Task SaveReport(string? fileContent, CancellationToken cancellationToken)
    {
        var reportContent = Encoding.UTF8.GetBytes(fileContent ?? "");
        await FileHelper.SaveAsync(Constants.HtmlReportFilePath, reportContent, cancellationToken);
    }

    private static async Task<Result<string>> GenerateHtmlReport(ReportGeneratorContext reportContext,
        CancellationToken cancellationToken)
    {
        var reportTemplate = await Task.Run(() => Handlebars.Compile(Resources.report_template), cancellationToken);

        var currentDate = DateTime.UtcNow;
        var commitsPath =
            @$"{reportContext.ReportSettings.EmployeeFolderName}\{currentDate.Year}\{currentDate.Month:00}\{Constants.CommitHistoryFileName}";

        var htmlReport = reportTemplate(new
        {
            month_name = DatetimeHelper.GetCurrentMonthName(),
            working_days = reportContext.WorkingDays,
            absences_days = reportContext.AbsencesDays,
            project_name = reportContext.ReportSettings.ProjectName,
            employee_fullname = reportContext.ReportSettings.EmployeeFullName,
            employee_job_position = reportContext.ReportSettings.EmployeeJobPosition,
            employee_commits_path = commitsPath,
            controler_fullname = reportContext.ReportSettings.ControlerFullName,
            controler_job_position = reportContext.ReportSettings.ControlerJobPosition,
        });

        return htmlReport;
    }
}