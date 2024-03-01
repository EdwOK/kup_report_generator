using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helpers;
using KUPReportGenerator.GitCommitsHistory.DataProviders;

namespace KUPReportGenerator.Report;

public record ReportSettings
{
    public string EmployeeFullName { get; set; } = null!;

    public string EmployeeEmail { get; set; } = null!;

    public string EmployeeJobPosition { get; set; } = null!;

    public string EmployeeFolderName { get; set; } = null!;

    public string ControlerFullName { get; set; } = null!;

    public string ControlerJobPosition { get; set; } = null!;

    public string ProjectName { get; set; } = null!;

    public string? ProjectAdoOrganizationName { get; set; } = null!;

    public string? ProjectGitDirectory { get; set; }

    public string? RapidApiKey { get; set; }

    public GitCommitsHistoryProvider GitCommitHistoryProvider { get; set; } = GitCommitsHistoryProvider.AzureDevOps;

    public async Task<Result<ReportSettings>> SaveAsync(string filePath, CancellationToken cancellationToken)
    {
        var error = new Error("Couldn't save the report settings file.");

        try
        {
            await using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, this, cancellationToken: cancellationToken,
                jsonTypeInfo: SourceGenerationContext.Default.ReportSettings);
        }
        catch (Exception exc)
        {
            return error.CausedBy(exc);
        }

        return Result.Ok(this);
    }

    public static async Task<Result<ReportSettings>> OpenAsync(string filePath, CancellationToken cancellationToken)
    {
        var error = new Error("Couldn't read the report settings file.");

        var reportSettingsText = await FileHelper.ReadAsync(filePath, cancellationToken);
        if (reportSettingsText.IsFailed)
        {
            return error.CausedBy(reportSettingsText.Errors);
        }

        ReportSettings? reportSettings;

        try
        {
            await using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(reportSettingsText.Value));
            reportSettings = await JsonSerializer.DeserializeAsync(memoryStream, cancellationToken: cancellationToken,
                jsonTypeInfo: SourceGenerationContext.Default.ReportSettings);

            if (reportSettings is null)
            {
                return error.CausedBy("Failed to parse JSON from the report file settings.");
            }
        }
        catch (Exception exc)
        {
            return error.CausedBy(exc);
        }

        return Result.Ok(reportSettings);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ReportSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext;