namespace KUPReportGenerator;

public static class Constants
{
    public static readonly string CurrentDirectory = Directory.GetCurrentDirectory();

    public static readonly string ResourceDirectory = Path.Combine(CurrentDirectory, "Resources");
    public static readonly string OutputDirectory = Path.Combine(CurrentDirectory, "Output");

    public const string SettingsFileName = "settings.json";
    public static readonly string SettingsFilePath = Path.Combine(CurrentDirectory, SettingsFileName);

    public const string CommitHistoryFileName = "Commits.txt";
    public static readonly string CommitsHistoryFilePath = Path.Combine(OutputDirectory, CommitHistoryFileName);

    public const string HtmlReportFileName = "report.html";
    public static readonly string HtmlReportFilePath = Path.Combine(OutputDirectory, HtmlReportFileName);

    public const string PdfReportFileName = "report.pdf";
    public static readonly string PdfReportFilePath = Path.Combine(OutputDirectory, PdfReportFileName);
}