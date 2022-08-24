namespace KUPReportGenerator;

public static class Constants
{
    private static readonly string CurrentDirectory = Directory.GetCurrentDirectory();
   
    public static readonly string ResourceDirectory = Path.Combine(CurrentDirectory, "Resources");
    public static readonly string OutputDirectory = Path.Combine(CurrentDirectory, "Output");
    
    public const string SettingsFileName = "settings.json";
    public static readonly string SettingsFilePath = Path.Combine(CurrentDirectory, SettingsFileName);
    
    public const string CommitHistoryFileName = "Commits.txt";
    public static readonly string CommitsHistoryFilePath = Path.Combine(OutputDirectory, CommitHistoryFileName);
    
    public const string ReportFileName = "report.html";
    public static readonly string ReportFilePath = Path.Combine(OutputDirectory, ReportFileName);
    
    public const string ReportTemplateFileName = "report_template.html";
    public static readonly string ReportTemplateFilePath = Path.Combine(ResourceDirectory, ReportTemplateFileName);
}