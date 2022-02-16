using FluentResults;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KUPReportGenerator.Helpers;

namespace KUPReportGenerator;

[Serializable]
public record ReportSettings
{
    [Required]
    public string EmployeeFullName { get; init; } = null!;

    [Required]
    public string[] EmployeeCommitsAuthors { get; init; } = null!;

    [Required]
    public bool EmployeeHasCommitsHistory { get; init; } = false;

    [Required]
    public string EmployeePosition { get; init; } = null!;

    [Required]
    public string EmployeeFolderName { get; init; } = null!;

    [Required]
    public string ControlerFullName { get; init; } = null!;

    [Required]
    public string ControlerPosition { get; init; } = null!;

    [Required]
    public string ProjectName { get; init; } = null!;

    [Required]
    public string ProjectRootFolder { get; init; } = null!;

    [Required]
    public ushort WorkingDays { get; init; }

    [Required]
    public ushort AbsencesDays { get; init; }

    public static async Task<Result<ReportSettings>> ParseAsync(string settingsFilePath,
        CancellationToken cancellationToken)
    {
        var reportSettingsText = await FileHelper.ReadAsync(settingsFilePath, cancellationToken);
        if (reportSettingsText.IsFailed)
        {
            return reportSettingsText.ToResult();
        }

        ReportSettings? reportSettings;

        try
        {
            await using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(reportSettingsText.Value));
            reportSettings = await JsonSerializer.DeserializeAsync<ReportSettings>(memoryStream,
                cancellationToken: cancellationToken);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Invalid report settings.").CausedBy(exc));
        }

        if (reportSettings is null)
        {
            return Result.Fail("Invalid report settings.");
        }

        return Result.Ok(reportSettings);
    }
}