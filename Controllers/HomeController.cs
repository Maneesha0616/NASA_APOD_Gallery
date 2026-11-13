using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;
using NasaApodGallery.Data;
using NasaApodGallery.Models;
using NasaApodGallery.Services;

namespace NasaApodGallery.Controllers;

public class HomeController : Controller
{
    public const string ImageProxyClientName = "ApodImageProxy";

    private const int MaxImageProxyRedirects = 3;
    private const long MaxImageProxyBytes = 25 * 1024 * 1024;

    private readonly INasaApodService _nasaApodService;
    private readonly IApodRepository _apodRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        INasaApodService nasaApodService,
        IApodRepository apodRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<HomeController> logger)
    {
        _nasaApodService = nasaApodService;
        _apodRepository = apodRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Displays all saved APOD records in a responsive gallery layout.
    /// Supports keyword search, media type filter, and sort order.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? mediaType = "all", string? sort = "desc")
    {
        try
        {
            var entries = await _apodRepository.GetAllEntriesAsync(search, mediaType, sort);
            var totalCount = await _apodRepository.GetTotalCountAsync();

            var viewModel = new GalleryViewModel
            {
                Entries = entries,
                SearchTerm = search,
                MediaTypeFilter = mediaType,
                SortOrder = sort,
                TotalSavedCount = totalCount,
                FetchRequest = new FetchRequestViewModel
                {
                    StartDate = DateTime.Today.AddDays(-7),
                    EndDate = DateTime.Today
                }
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading gallery entries from database.");
            ViewBag.ErrorMessage = $"Failed to load saved records from database: {ex.Message}";
            return View(new GalleryViewModel());
        }
    }

    /// <summary>
    /// Consumes the NASA APOD API for the specified date range and persists new entries using ADO.NET.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fetch([Bind(Prefix = "FetchRequest")] FetchRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            TempData["ErrorMessage"] = $"Invalid date parameters: {errors}";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var startDate = model.StartDate!.Value;
            var endDate = model.EndDate!.Value;

            _logger.LogInformation("Initiating NASA APOD fetch for {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}", startDate, endDate);

            var dtos = await _nasaApodService.FetchApodRangeAsync(startDate, endDate);

            if (dtos.Count == 0)
            {
                TempData["WarningMessage"] = $"NASA returned 0 records for the date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.";
                return RedirectToAction(nameof(Index));
            }

            var (inserted, duplicates) = await _apodRepository.SaveApodEntriesAsync(dtos);

            if (inserted > 0)
            {
                TempData["SuccessMessage"] = $"Successfully fetched {dtos.Count} record(s) from NASA: {inserted} new entry(ies) saved to database, {duplicates} duplicate(s) skipped.";
            }
            else
            {
                TempData["InfoMessage"] = $"Fetched {dtos.Count} record(s) from NASA. All {duplicates} were already present in your database (no new duplicates added).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch or save APOD entries.");
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Quick fetch shortcut for fetching recent N days (e.g. 7 or 14 days).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickFetch(int days = 7)
    {
        if (days < 1) days = 7;
        if (days > 30) days = 30;

        var request = new FetchRequestViewModel
        {
            StartDate = DateTime.Today.AddDays(-days),
            EndDate = DateTime.Today
        };

        return await Fetch(request);
    }

    /// <summary>
    /// Displays detailed view of a single APOD item.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var entry = await _apodRepository.GetEntryByIdAsync(id);
            if (entry == null)
            {
                TempData["ErrorMessage"] = $"No APOD entry found with ID #{id}.";
                return RedirectToAction(nameof(Index));
            }

            return View(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving APOD record #{Id}", id);
            TempData["ErrorMessage"] = $"Error loading APOD record: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Proxies external astronomy images through the local server to bypass CORS, Referrer policies, and mixed-content restrictions in browsers.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> ImageProxy([FromQuery] string url)
    {
        var target = await ResolveSafeImageTargetAsync(url);
        if (target is null)
        {
            return BadRequest();
        }

        var client = _httpClientFactory.CreateClient(ImageProxyClientName);

        for (var hop = 0; hop <= MaxImageProxyRedirects; hop++)
        {
            HttpResponseMessage response;
            try
            {
                // Referer set to the image's own origin so hotlink protection on apod.nasa.gov passes.
                using var request = new HttpRequestMessage(HttpMethod.Get, target);
                request.Headers.Referrer = new Uri($"{target.Scheme}://{target.Host}/");

                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "ImageProxy could not reach {Url}", target);
                return NotFound();
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
                {
                    // Re-validate the redirect target so an approved host cannot bounce the request internally.
                    var redirect = await ResolveSafeImageTargetAsync(new Uri(target, response.Headers.Location).ToString());
                    if (redirect is null)
                    {
                        return BadRequest();
                    }

                    target = redirect;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ImageProxy: origin returned {Status} for {Url}", response.StatusCode, target);
                    return NotFound();
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("ImageProxy: refusing non-image content type '{ContentType}' from {Url}", contentType, target);
                    return NotFound();
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0)
                {
                    return NotFound();
                }

                if (bytes.Length > MaxImageProxyBytes)
                {
                    _logger.LogWarning("ImageProxy: payload of {Bytes} bytes exceeds limit for {Url}", bytes.Length, target);
                    return StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                return File(bytes, contentType);
            }
        }

        _logger.LogWarning("ImageProxy: redirect limit reached for {Url}", url);
        return NotFound();
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved
            or HttpStatusCode.Found
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    /// <summary>
    /// Confirms the proxy target is a public http/https URL, so the endpoint cannot be used to reach
    /// loopback, private, link-local, or otherwise reserved addresses on the host network.
    /// Returns null when the URL must not be fetched.
    /// </summary>
    private static async Task<Uri?> ResolveSafeImageTargetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var candidate))
        {
            return null;
        }

        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (string.IsNullOrEmpty(candidate.Host))
        {
            return null;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(candidate.Host);
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            return null;
        }

        if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
        {
            return null;
        }

        return candidate;
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address)) return true;
            if (bytes.All(b => b == 0)) return true;                  // :: unspecified
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true; // fe80::/10 link-local
            return (bytes[0] & 0xFE) == 0xFC || bytes[0] >= 0xFF;    // fc00::/7 unique-local, ff00::/8 multicast
        }

        if (IPAddress.IsLoopback(address)) return true;

        return bytes[0] switch
        {
            0 or 10 or 127 => true,                          // this-network, RFC1918, loopback
            100 => bytes[1] >= 64 && bytes[1] <= 127,        // carrier-grade NAT
            169 => bytes[1] == 254,                          // link-local
            172 => bytes[1] >= 16 && bytes[1] <= 31,         // RFC1918
            192 => bytes[1] == 168 || (bytes[1] == 0 && bytes[2] is 0 or 2), // private, IETF, TEST-NET-1
            198 => bytes[1] is 18 or 19 || (bytes[1] == 51 && bytes[2] == 100), // benchmarking, TEST-NET-2
            203 => bytes[1] == 0 && bytes[2] == 114,         // TEST-NET-3
            >= 224 => true,                                  // multicast and reserved
            _ => false
        };
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
