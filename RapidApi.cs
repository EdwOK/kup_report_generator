using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using KUPReportGenerator.Helpers;

namespace KUPReportGenerator;

internal class RapidApi : IDisposable
{
    private readonly HttpClient _httpClient;

    public RapidApi(string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://working-days.p.rapidapi.com")
        };
        _httpClient.DefaultRequestHeaders.ExpectContinue = false;
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Host", "working-days.p.rapidapi.com");
        _httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Key", apiKey);
    }

    public async Task<Result<ushort>> GetWorkingDays(string countryCode = "PL", CancellationToken cancellationToken = default)
    {
        var startDate = DatetimeHelper.GetFirstDateOfMonth();
        var lastDate = DatetimeHelper.GetLastDateOfMonth();

        var jsonNode = await Result.Try(() =>
            _httpClient.GetFromJsonAsync<JsonNode>(
                $"analyse?country_code={countryCode}&start_date={startDate:yyyy-MM-dd}&end_date={lastDate:yyyy-MM-dd}",
                cancellationToken));

        if (jsonNode.IsFailed)
        {
            return jsonNode.ToResult();
        }

        var workingDaysResult = Result.Try(() => jsonNode.ValueOrDefault?["result"]?["working_days"]?["total"]?.ToString());
        if (!ushort.TryParse(workingDaysResult.ValueOrDefault, out var workingDays))
        {
            return Result.Fail("Couldn't get working days from the API error, please check the response.")
                .WithErrors(workingDaysResult.Errors);
        }

        return Result.Ok(workingDays);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}