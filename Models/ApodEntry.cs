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
    /// If media_type is video and thumbnail_url exists, returns thumbnail_url; otherwise url.
    /// </summary>
    public string DisplayImageUrl =>
        !string.IsNullOrWhiteSpace(ThumbnailUrl) ? ThumbnailUrl : Url;

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

