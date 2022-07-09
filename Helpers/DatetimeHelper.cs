namespace KUPReportGenerator.Helpers;

internal static class DatetimeHelper
{
    public static DateTime GetFirstDateOfMonth()
    {
        var currentDate = DateTime.UtcNow;
        return new DateTime(currentDate.Year, currentDate.Month, 1);
    }

    public static DateTime GetLastDateOfMonth()
    {
        return GetFirstDateOfMonth().AddMonths(1).AddDays(-1);
    }
}
