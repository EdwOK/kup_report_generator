using FluentResults;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KUPReportGenerator.Helpers;

namespace KUPReportGenerator;

public record ReportSettings
{
    [Required]
    public string EmployeeFullName { get; set; } = null!;

    [Required]
    public string[] EmployeeCommitsAuthors { get; set; } = null!;

    [Required]
    public bool EmployeeHasCommitsHistory { get; set; } = false;

    [Required]
    public string EmployeePosition { get; set; } = null!;

    [Required]
    public string EmployeeFolderName { get; set; } = null!;

    [Required]
    public string ControlerFullName { get; set; } = null!;

    [Required]
    public string ControlerPosition { get; set; } = null!;

    [Required]
    public string ProjectName { get; set; } = null!;

    [Required]
    public string ProjectRootFolder { get; set; } = null!;

    public ushort? WorkingDays { get; set; }

    [Required]
    public ushort AbsencesDays { get; set; }

    public string? RapidApiKey { get; set; }

    public static async Task<Result<ReportSettings>> OpenAsync(string settingsFilePath,
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
            reportSettings = await JsonSerializer.DeserializeAsync(memoryStream,
                cancellationToken: cancellationToken, jsonTypeInfo: SourceGenerationContext.Default.ReportSettings);

            if (reportSettings is not null && reportSettings.WorkingDays is null or 0)
            {
                if (reportSettings.RapidApiKey is null)
                {
                    return Result.Fail("Please set the number of working days in \"WorkingDays\" or the received API key from https://rapidapi.com/joursouvres-api/api/working-days/ in \"RapidApiKey\".");
                }

                using var rapidApi = new RapidApi(reportSettings.RapidApiKey!);

                var workingDays = await rapidApi.GetWorkingDays(cancellationToken: cancellationToken);
                if (workingDays.IsFailed)
                {
                    return workingDays.ToResult();
                }

                reportSettings.WorkingDays = workingDays.Value;
            }
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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ReportSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}