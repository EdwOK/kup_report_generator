using KUPReportGenerator.Helpers;

namespace KUPReportGenerator;

public static class Constants
{
    public static readonly string CurrentDirectory = Directory.GetCurrentDirectory();

    public static readonly string ResourceDirectory = Path.Combine(CurrentDirectory, "Resources");
    public static readonly string OutputDirectory = Path.Combine(CurrentDirectory, "Output");

    public static readonly string SettingsFileName = $"settings.json";
    public static readonly string SettingsFilePath = Path.Combine(CurrentDirectory, SettingsFileName);

    public static readonly string CommitHistoryFileName = $"Commits_{DatetimeHelper.GetCurrentMonthNameWithYear()}.txt";
    public static readonly string CommitsHistoryFilePath = Path.Combine(OutputDirectory, CommitHistoryFileName);

    public static readonly string HtmlReportFileName = $"report_{DatetimeHelper.GetCurrentMonthNameWithYear()}.html";
    public static readonly string HtmlReportFilePath = Path.Combine(OutputDirectory, HtmlReportFileName);

    public static readonly string PdfReportFileName = $"report_{DatetimeHelper.GetCurrentMonthNameWithYear()}.pdf";
    public static readonly string PdfReportFilePath = Path.Combine(OutputDirectory, PdfReportFileName);
}