using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using KUPReportGenerator.Helpers;

namespace KUPReportGenerator;

public record ReportSettings
{
    [Required]
    public string EmployeeFullName { get; set; } = null!;

    [Required]
    public string EmployeeEmail { get; set; } = null!;

    [Required]
    public string EmployeeJobPosition { get; set; } = null!;

    [Required]
    public string EmployeeFolderName { get; set; } = null!;

    [Required]
    public string ControlerFullName { get; set; } = null!;

    [Required]
    public string ControlerJobPosition { get; set; } = null!;

    [Required]
    public string ProjectName { get; set; } = null!;

    [Required]
    public string ProjectAdoOrganizationName { get; set; } = null!;

    [JsonIgnore]
    public ushort AbsencesDays { get; set; }

    [JsonIgnore]
    public ushort? WorkingDays { get; set; }

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
            reportSettings = await JsonSerializer.DeserializeAsync(memoryStream, cancellationToken: cancellationToken,
                jsonTypeInfo: SourceGenerationContext.Default.ReportSettings);
            if (reportSettings is null)
            {
                throw new InvalidOperationException("Failed to parse JSON from the report file settings.");
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