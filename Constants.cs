using System.IO;

namespace KUPReportGenerator
{
    public static class Constants
    {
        private static readonly string CurrentDirectory = Directory.GetCurrentDirectory();
     
        public static readonly string SettingsFilePath = Path.Combine(CurrentDirectory, "settings.json");
        
        private static readonly string ResourceDirectory = Path.Combine(CurrentDirectory, "Resources");
        public static readonly string ReportTemplateFilePath = Path.Combine(ResourceDirectory, "report_template.html");
        
        public static readonly string OutputDirectory = Path.Combine(CurrentDirectory, "Output");
        public static readonly string CommitsHistoryFilePath = Path.Combine(OutputDirectory, "Commits.txt");
        public static readonly string ReportFilePath = Path.Combine(OutputDirectory, "report.html");
    }
}