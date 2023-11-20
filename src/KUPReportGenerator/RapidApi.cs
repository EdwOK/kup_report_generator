using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

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
            var rapidApiResponse = await _httpClient.GetFromJsonAsync(
                $"analyse?country_code={countryCode}&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}",
                jsonTypeInfo: RapidApiResponseJsonContext.Default.RapidApiResponse,
                cancellationToken);

            if (!ushort.TryParse(rapidApiResponse?.Result?.WorkingDays?.Total, out var workingDays))
            {
                return Result.Fail("No monthly working days were found.");
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

internal record RapidApiResponse
{
    [JsonPropertyName("result")]
    public RapidApiResult Result { get; init; } = new();

    public record RapidApiResult
    {
        [JsonPropertyName("working_days")]
        public RapidApiWorkingDays WorkingDays { get; init; } = new();

        public record RapidApiWorkingDays
        {
            [JsonPropertyName("total")]
            public string Total { get; init; } = null!;
        }
    }
}

[JsonSerializable(typeof(RapidApiResponse))]
internal partial class RapidApiResponseJsonContext : JsonSerializerContext { }