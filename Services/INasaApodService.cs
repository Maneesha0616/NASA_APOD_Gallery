using NasaApodGallery.DTOs;

namespace NasaApodGallery.Services;

public interface INasaApodService
{
    /// <summary>
    /// Fetches APOD entries for a given date range from NASA's REST API and deserializes the JSON response into a list of DTOs.
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of deserialized NasaApodDto objects</returns>
    Task<List<NasaApodDto>> FetchApodRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
