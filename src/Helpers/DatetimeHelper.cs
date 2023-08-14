using System.Globalization;

namespace KUPReportGenerator.Helpers;

public static class DatetimeHelper
{
    public static string GetCurrentMonthNameWithYear() => DateTime.UtcNow.ToString("MM_yyyy", CultureInfo.InvariantCulture);

    public static string GetCurrentMonthName() => DateTime.UtcNow.ToString("MMMM", CultureInfo.InvariantCulture);

    public static string[] GetAllMonthNames() => CultureInfo.InvariantCulture.DateTimeFormat.MonthGenitiveNames[..12];

    public static DateTime GetFirstDateOfMonth(string monthName)
    {
        var currentDate = DateTime.UtcNow;
        var month = DateTime.ParseExact(monthName, "MMMM", CultureInfo.InvariantCulture).Month;
        return new DateTime(currentDate.Year, month, 1);
    }

    public static DateTime GetLastDateOfMonth(string monthName) => GetFirstDateOfMonth(monthName).AddMonths(1).AddDays(-1);
}