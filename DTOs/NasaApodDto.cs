using System.Text.Json.Serialization;

namespace NasaApodGallery.DTOs;

/// <summary>
/// Data Transfer Object representing the JSON response from NASA's Astronomy Picture of the Day (APOD) API.
/// </summary>
public class NasaApodDto
{
    [JsonPropertyName("copyright")]
    public string? Copyright { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }

    [JsonPropertyName("hdurl")]
    public string? HdUrl { get; set; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = "image";

    [JsonPropertyName("service_version")]
    public string? ServiceVersion { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }
}
