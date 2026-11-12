using NasaApodGallery.DTOs;
using NasaApodGallery.Models;

namespace NasaApodGallery.Data;

public interface IApodRepository
{
    /// <summary>
    /// Stores a collection of NASA APOD DTO entries into the SQL database using ADO.NET.
    /// Inserts only new entries to prevent duplicates.
    /// </summary>
    /// <returns>A tuple of (insertedCount, duplicateCount)</returns>
    Task<(int inserted, int duplicates)> SaveApodEntriesAsync(IEnumerable<NasaApodDto> entries);

    /// <summary>
    /// Retrieves all APOD records from the database using ADO.NET with optional search, media filter, and sort order.
    /// </summary>
    Task<List<ApodEntry>> GetAllEntriesAsync(string? searchTerm = null, string? mediaType = null, string? sortOrder = "desc");

    /// <summary>
    /// Retrieves a single APOD entry by its database ID using ADO.NET.
    /// </summary>
    Task<ApodEntry?> GetEntryByIdAsync(int id);

    /// <summary>
    /// Retrieves the total count of saved APOD records in the database.
    /// </summary>
    Task<int> GetTotalCountAsync();
}

