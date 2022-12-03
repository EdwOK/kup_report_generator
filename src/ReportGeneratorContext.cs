namespace KUPReportGenerator;

public record ReportGeneratorContext(ReportSettings ReportSettings, ushort? AbsencesDays, ushort? WorkingDays);