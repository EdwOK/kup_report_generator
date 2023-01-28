namespace KUPReportGenerator.Report;

public record ReportGeneratorContext(ReportSettings ReportSettings, ushort? AbsencesDays, ushort? WorkingDays);