namespace KUPReportGenerator.Report;

public record ReportGeneratorContext(ReportSettings ReportSettings, string WorkingMonth, ushort? AbsencesDays, ushort? WorkingDays);