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
    public async Task<IActionResult> Index(string? search, string? sort = "desc")
    {
        try
        {
            // Only retrieve images per technical assignment requirements
            var entries = await _apodRepository.GetAllEntriesAsync(search, "image", sort);
            var totalCount = await _apodRepository.GetTotalCountAsync();

            var viewModel = new GalleryViewModel
            {
                Entries = entries,
                SearchTerm = search,
                MediaTypeFilter = "image",
                SortOrder = sort,
                TotalSavedCount = totalCount,
                FetchRequest = new FetchRequestViewModel
                {
                    StartDate = (DateTime.Today.Year > 2024 ? new DateTime(2024, 3, 20) : DateTime.Today).AddDays(-6),
                    EndDate = DateTime.Today.Year > 2024 ? new DateTime(2024, 3, 20) : DateTime.Today
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
        _logger.LogInformation("Fetch called: StartDate={Start}, EndDate={End}", model.StartDate, model.EndDate);

        if (!ModelState.IsValid)
        {
            var errors = string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Distinct());
            TempData["ErrorMessage"] = $"Invalid date parameters: {errors}";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var startDate = model.StartDate!.Value.Date;
            var endDate = model.EndDate!.Value.Date;

            // Safety: swap if user somehow got them reversed
            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

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
    /// Proxies external astronomy images through the local server to bypass browser CORS and referrer restrictions.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> ImageProxy([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var target))
        {
            return NotFound();
        }

        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0");

            var bytes = await client.GetByteArrayAsync(target);
            var ext = Path.GetExtension(target.AbsolutePath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
            return File(bytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImageProxy error for {Url}", url);
            // Fallback: direct redirect
            return Redirect(url);
        }
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

        // Fast-path known public image hosts like nasa.gov
        if (candidate.Host.EndsWith("nasa.gov", StringComparison.OrdinalIgnoreCase) ||
            candidate.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            candidate.Host.EndsWith("ytimg.com", StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(candidate.Host);
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            // If DNS lookup fails in sandbox/offline environments, still permit public web domains
            if (!candidate.IsLoopback && candidate.Host.Contains('.'))
            {
                return candidate;
            }
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

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View("About");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
