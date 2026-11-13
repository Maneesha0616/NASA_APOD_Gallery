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

        _apiKey = string.IsNullOrWhiteSpace(configuration["NasaApi:ApiKey"])
            ? "DEMO_KEY"
            : configuration["NasaApi:ApiKey"]!.Trim();
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
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "NASA APOD API request timed out. Falling back to WordPress feed.");
            return await FetchFromNasaWordpressFeedAsync(startDate, endDate, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error connecting to NASA APOD API. Falling back to WordPress feed.");
            return await FetchFromNasaWordpressFeedAsync(startDate, endDate, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("NASA APOD API returned status {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

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

            // For server errors (500, 502, 503) and other failures, try the WordPress fallback
            _logger.LogInformation("Legacy API failed ({StatusCode}). Falling back to NASA science.nasa.gov feed.", response.StatusCode);
            return await FetchFromNasaWordpressFeedAsync(startDate, endDate, cancellationToken);
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
                var list = JsonSerializer.Deserialize<List<NasaApodDto>>(json, options) ?? new List<NasaApodDto>();
                // Only keep genuine astronomy images, filtering out NASA placeholder logos
                var genuine = list.Where(x => 
                    string.Equals(x.MediaType, "image", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Title, "NASA Science", StringComparison.OrdinalIgnoreCase) &&
                    !(x.Url != null && x.Url.Contains("nasa-logo", StringComparison.OrdinalIgnoreCase))
                ).ToList();

                if (genuine.Count > 0)
                {
                    return genuine;
                }

                // If legacy api.nasa.gov returned only placeholder logos (NASA GitHub Issue #184),
                // automatically fallback to NASA's official modern APOD feed on science.nasa.gov
                return await FetchFromNasaWordpressFeedAsync(startDate, endDate, cancellationToken);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Fallback if NASA API returned a single object for single-day range
                var single = JsonSerializer.Deserialize<NasaApodDto>(json, options);
                if (single != null && 
                    string.Equals(single.MediaType, "image", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(single.Title, "NASA Science", StringComparison.OrdinalIgnoreCase) &&
                    !(single.Url != null && single.Url.Contains("nasa-logo", StringComparison.OrdinalIgnoreCase)))
                {
                    return new List<NasaApodDto> { single };
                }
                return await FetchFromNasaWordpressFeedAsync(startDate, endDate, cancellationToken);
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

    /// <summary>
    /// Fallback query against NASA's official modern APOD feed (science.nasa.gov/wp-json/wp/v2/apod-basic/{yyMMdd}),
    /// which provides real astronomical images for any date when api.nasa.gov is experiencing Issue #184 scraper degradation.
    /// </summary>
    private async Task<List<NasaApodDto>> FetchFromNasaWordpressFeedAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var results = new List<NasaApodDto>();
        try
        {
            var dateList = new List<DateTime>();
            for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
            {
                dateList.Add(d);
            }

            using var semaphore = new SemaphoreSlim(6);
            var tasks = dateList.Select(async d =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var yymmdd = d.ToString("yyMMdd");
                    var itemUrl = $"https://science.nasa.gov/wp-json/wp/v2/apod-basic/{yymmdd}";
                    using var req = new HttpRequestMessage(HttpMethod.Get, itemUrl);
                    req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) NASA-APOD-Client");
                    var res = await _httpClient.SendAsync(req, cancellationToken);
                    if (!res.IsSuccessStatusCode) return null;

                    var json = await res.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return null;

                    var dateStr = root.TryGetProperty("date", out var dProp) ? dProp.GetString() ?? "" : d.ToString("yyyy-MM-dd");
                    var title = root.TryGetProperty("title", out var tProp) ? CleanHtml(tProp.GetString()) : "";
                    var explanation = root.TryGetProperty("explanation", out var expProp) ? CleanHtml(expProp.GetString()) : "";
                    var mediaType = root.TryGetProperty("media_type", out var mProp) ? mProp.GetString() ?? "image" : "image";
                    var hdurl = root.TryGetProperty("hdurl", out var hdProp) ? hdProp.GetString() ?? "" : "";
                    var rawUrl = root.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : "";
                    var directImageUrl = !string.IsNullOrWhiteSpace(hdurl) ? hdurl : rawUrl;
                    var copyright = root.TryGetProperty("copyright", out var cProp) ? CleanHtml(cProp.GetString()) : "";

                    if (!string.Equals(mediaType, "image", StringComparison.OrdinalIgnoreCase)) return null;

                    return new NasaApodDto
                    {
                        Date = dateStr,
                        Title = title,
                        Explanation = explanation,
                        MediaType = mediaType,
                        HdUrl = hdurl,
                        Url = directImageUrl,
                        Copyright = copyright,
                        ServiceVersion = "v1"
                    };
                }
                catch
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var fetched = await Task.WhenAll(tasks);
            results.AddRange(fetched.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Url))!);
            return results.OrderByDescending(x => x.Date).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallback NASA WordPress date lookup failed.");
            return results;
        }
    }

    private static string CleanHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var stripped = System.Text.RegularExpressions.Regex.Replace(input, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(stripped).Trim();
    }
}

