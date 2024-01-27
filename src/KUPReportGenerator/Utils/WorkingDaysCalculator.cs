using Helpers;

namespace KUPReportGenerator.Utils;

internal class WorkingDaysCalculator
{
    private const ushort DefaultWorkingDays = 21;

    public static async Task<Result<ushort>> GetWorkingDaysInMonth(string monthName, string? rapidApiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(rapidApiKey))
        {
            return DefaultWorkingDays;
        }

        var startDate = DatetimeHelper.GetFirstDateOfMonth(monthName);
        var endDate = DatetimeHelper.GetLastDateOfMonth(monthName);

        using var rapidApi = new RapidApi(rapidApiKey);

        var workingDays = await rapidApi.GetWorkingDays(startDate, endDate,
            cancellationToken: cancellationToken);
        return workingDays;
    }
}