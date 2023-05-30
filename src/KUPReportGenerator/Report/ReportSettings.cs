using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using KUPReportGenerator.Helpers;

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

    public string ProjectAdoOrganizationName { get; set; } = null!;

    public string? RapidApiKey { get; set; }

    public async Task<Result<ReportSettings>> SaveAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, this, cancellationToken: cancellationToken,
                jsonTypeInfo: SourceGenerationContext.Default.ReportSettings);
            await fileStream.DisposeAsync();
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Couldn't save the report settings file.").CausedBy(exc));
        }

        return Result.Ok(this);
    }

    public static async Task<Result<ReportSettings>> OpenAsync(string filePath, CancellationToken cancellationToken)
    {
        var reportSettingsText = await FileHelper.ReadAsync(filePath, cancellationToken);
        if (reportSettingsText.IsFailed)
        {
            return reportSettingsText.ToResult();
        }

        ReportSettings? reportSettings;

        try
        {
            await using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(reportSettingsText.Value));
            reportSettings = await JsonSerializer.DeserializeAsync<ReportSettings>(memoryStream, cancellationToken: cancellationToken,
                jsonTypeInfo: SourceGenerationContext.Default.ReportSettings);
            if (reportSettings is null)
            {
                return Result.Fail("Failed to parse JSON from the report file settings.");
            }
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Couldn't read the report settings file.").CausedBy(exc));
        }

        return Result.Ok(reportSettings);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ReportSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}