namespace NasaApodGallery.Models;

public class GalleryViewModel
{
    /// <summary>
    /// Saved APOD entries retrieved from SQL Server.
    /// </summary>
    public List<ApodEntry> Entries { get; set; } = new();

    /// <summary>
    /// Optional search keyword to filter by title or explanation.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Optional filter for media type ('all', 'image', 'video').
    /// </summary>
    public string? MediaTypeFilter { get; set; } = "all";

    /// <summary>
    /// Sort order ('desc' or 'asc').
    /// </summary>
    public string? SortOrder { get; set; } = "desc";

    /// <summary>
    /// Total count of entries in the database.
    /// </summary>
    public int TotalSavedCount { get; set; }

    /// <summary>
    /// Total filtered entries being displayed.
    /// </summary>
    public int DisplayedCount => Entries.Count;

    /// <summary>
    /// Model for fetching a date range from NASA APOD API.
    /// </summary>
    public FetchRequestViewModel FetchRequest { get; set; } = new();
}
