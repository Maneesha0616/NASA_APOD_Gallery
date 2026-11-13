using System.ComponentModel.DataAnnotations;

namespace NasaApodGallery.Models;

/// <summary>
/// Domain model representing a stored NASA APOD entry retrieved from the SQL database.
/// </summary>
public class ApodEntry
{
    public int Id { get; set; }

    [Display(Name = "Date")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime Date { get; set; }

    [Required]
    [StringLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Explanation { get; set; }

    [Required]
    [Url]
    [StringLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Url]
    [StringLength(2048)]
    public string? HdUrl { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Media Type")]
    public string MediaType { get; set; } = "image";

    [StringLength(50)]
    [Display(Name = "Service Version")]
    public string? ServiceVersion { get; set; }

    [StringLength(255)]
    public string? Copyright { get; set; }

    [Url]
    [StringLength(2048)]
    public string? ThumbnailUrl { get; set; }

    [Display(Name = "Saved At")]
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// Returns the best URL to display as thumbnail or primary visual element.
    /// Prefers the direct image source (HdUrl from assets.science.nasa.gov or direct .jpg/.png),
    /// avoiding webpage/article URLs.
    /// </summary>
    public string DisplayImageUrl
    {
        get
        {
            // If HdUrl is provided and looks like an image or NASA assets image, prefer it
            if (!string.IsNullOrWhiteSpace(HdUrl) && IsImageSource(HdUrl))
            {
                return HdUrl;
            }

            // If Url looks like an image, use it
            if (!string.IsNullOrWhiteSpace(Url) && IsImageSource(Url))
            {
                return Url;
            }

            // Fallback to whichever is non-empty
            return !string.IsNullOrWhiteSpace(HdUrl) ? HdUrl : (Url ?? string.Empty);
        }
    }

    private static bool IsImageSource(string u)
    {
        if (string.IsNullOrWhiteSpace(u)) return false;
        var clean = u.ToLowerInvariant();
        if (clean.Contains("/image-article/") || clean.EndsWith(".html") || clean.EndsWith(".htm"))
        {
            return false;
        }
        return clean.Contains(".jpg") || clean.Contains(".jpeg") || clean.Contains(".png") ||
               clean.Contains(".webp") || clean.Contains("dynamicimage") || clean.Contains("apod/image");
    }

    /// <summary>
    /// Checks whether the entry is an image.
    /// </summary>
    public bool IsImage =>
        string.Equals(MediaType, "image", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the entry is a video.
    /// </summary>
    public bool IsVideo =>
        string.Equals(MediaType, "video", StringComparison.OrdinalIgnoreCase);
}

