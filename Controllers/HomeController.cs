using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NasaApodGallery.Data;
using NasaApodGallery.Models;
using NasaApodGallery.Services;

namespace NasaApodGallery.Controllers;

public class HomeController : Controller
{
    private readonly INasaApodService _nasaApodService;
    private readonly IApodRepository _apodRepository;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        INasaApodService nasaApodService,
        IApodRepository apodRepository,
        ILogger<HomeController> logger)
    {
        _nasaApodService = nasaApodService;
        _apodRepository = apodRepository;
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
    public async Task<IActionResult> Fetch(FetchRequestViewModel model)
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
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return NotFound();
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return Redirect(url);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var stream = await response.Content.ReadAsStreamAsync();
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to proxy image: {Url}", url);
            return Redirect(url);
        }
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
