using System.Globalization;

namespace KUPReportGenerator.Helpers;

internal static class DatetimeHelper
{
    public static string GetCurrentMonthName() => DateTime.UtcNow.ToString("MMMM", CultureInfo.InvariantCulture);

    public static DateTime GetFirstDateOfCurrentMonth()
    {
        var currentDate = DateTime.UtcNow;
        return new DateTime(currentDate.Year, currentDate.Month, 1);
    }

    public static DateTime GetLastDateOfCurrentMonth() => GetFirstDateOfCurrentMonth().AddMonths(1).AddDays(-1);
}