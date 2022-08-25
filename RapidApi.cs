using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace KUPReportGenerator;

internal class RapidApi : IDisposable
{
    private readonly HttpClient _httpClient;

    public RapidApi(string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://working-days.p.rapidapi.com"),
            DefaultRequestHeaders =
            {
                ExpectContinue = true
            }
        };
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Host", "working-days.p.rapidapi.com");
        _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Key", apiKey);
    }

    public async Task<Result<ushort>> GetWorkingDays(DateTime startDate, DateTime endDate, string countryCode = "PL",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonNode = await _httpClient.GetFromJsonAsync<JsonNode>(
                $"analyse?country_code={countryCode}&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}",
                cancellationToken);

            var workingDaysResult = jsonNode?["result"]?["working_days"]?["total"]?.ToString();
            if (!ushort.TryParse(workingDaysResult, out var workingDays))
            {
                throw new InvalidOperationException("No monthly working days were found.");
            }

            return Result.Ok(workingDays);
        }
        catch (Exception exc)
        {
            return Result.Fail(new Error("Failed to get working days from the Rapid API.").CausedBy(exc));
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}