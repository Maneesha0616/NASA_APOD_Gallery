using System.Net;
using System.Text.Json;
using NasaApodGallery.DTOs;

namespace NasaApodGallery.Services;

public class NasaApodService : INasaApodService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<NasaApodService> _logger;

    public NasaApodService(HttpClient httpClient, IConfiguration configuration, ILogger<NasaApodService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["NasaApi:ApiKey"]?.Trim() ?? "DEMO_KEY";
        _baseUrl = configuration["NasaApi:BaseUrl"]?.Trim() ?? "https://api.nasa.gov/planetary/apod";
    }

    /// <summary>
    /// Fetches APOD data for a date range from NASA's REST API and deserializes the JSON array response.
    /// </summary>
    public async Task<List<NasaApodDto>> FetchApodRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date cannot be after end date.");
        }

        var startDateStr = startDate.ToString("yyyy-MM-dd");
        var endDateStr = endDate.ToString("yyyy-MM-dd");

        // Construct query string with start_date, end_date, thumbs=true, and api_key
        var requestUrl = $"{_baseUrl}?api_key={Uri.EscapeDataString(_apiKey)}&start_date={startDateStr}&end_date={endDateStr}&thumbs=true";

        _logger.LogInformation("Calling NASA APOD API: Range [{StartDate} to {EndDate}]", startDateStr, endDateStr);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while connecting to NASA APOD API.");
            throw new InvalidOperationException("Failed to reach NASA APOD API. Please check your internet connection.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("NASA APOD API returned status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException(
                    "NASA API rate limit exceeded (HTTP 429). If using DEMO_KEY, limits are 30 requests per hour and 50 per day. Please register for a free API key at https://api.nasa.gov and configure it in appsettings.json.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    $"NASA API rejected the request ({response.StatusCode}). Please verify that your API key in appsettings.json is valid.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new InvalidOperationException(
                    $"NASA API reported invalid parameters: {errorBody}");
            }

            throw new InvalidOperationException(
                $"NASA APOD API error ({response.StatusCode}): {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Attempt array deserialization (as required by requirement 2)
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<NasaApodDto>>(json, options);
                return list ?? new List<NasaApodDto>();
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Fallback if NASA API returned a single object for single-day range
                var single = JsonSerializer.Deserialize<NasaApodDto>(json, options);
                return single != null ? new List<NasaApodDto> { single } : new List<NasaApodDto>();
            }
            else
            {
                throw new InvalidOperationException("Unexpected JSON payload format received from NASA APOD API.");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON response from NASA APOD API: {Json}", json);
            throw new InvalidOperationException("Failed to deserialize the JSON response returned by the NASA APOD API.", ex);
        }
    }
}

