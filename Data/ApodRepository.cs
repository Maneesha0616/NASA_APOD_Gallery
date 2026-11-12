using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using NasaApodGallery.DTOs;
using NasaApodGallery.Models;

namespace NasaApodGallery.Data;

public class ApodRepository : IApodRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ApodRepository> _logger;

    public ApodRepository(IConfiguration configuration, ILogger<ApodRepository> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
    }

    /// <summary>
    /// Stores APOD entries using ADO.NET (SqlConnection, SqlCommand, SqlParameter).
    /// Inserts only new APOD entries by Date (avoids duplicates).
    /// </summary>
    public async Task<(int inserted, int duplicates)> SaveApodEntriesAsync(IEnumerable<NasaApodDto> entries)
    {
        int inserted = 0;
        int duplicates = 0;

        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM [dbo].[ApodEntries] WHERE [Date] = @Date)
            BEGIN
                INSERT INTO [dbo].[ApodEntries] (
                    [Date],
                    [Title],
                    [Explanation],
                    [Url],
                    [HdUrl],
                    [MediaType],
                    [ServiceVersion],
                    [Copyright],
                    [ThumbnailUrl],
                    [SavedAt]
                )
                VALUES (
                    @Date,
                    @Title,
                    @Explanation,
                    @Url,
                    @HdUrl,
                    @MediaType,
                    @ServiceVersion,
                    @Copyright,
                    @ThumbnailUrl,
                    SYSUTCDATETIME()
                );
                SELECT 1; -- Inserted
            END
            ELSE
            BEGIN
                SELECT 0; -- Duplicate skipped
            END";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Date))
            {
                continue;
            }

            if (!DateTime.TryParseExact(entry.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) &&
                !DateTime.TryParse(entry.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                _logger.LogWarning("Unable to parse APOD date '{Date}'. Skipping entry.", entry.Date);
                continue;
            }

            await using var command = new SqlCommand(sql, connection);

            // Parameterized query parameters to prevent SQL injection and ensure type safety
            command.Parameters.Add("@Date", SqlDbType.Date).Value = parsedDate.Date;
            command.Parameters.Add("@Title", SqlDbType.NVarChar, 500).Value = Truncate(entry.Title, 500);
            command.Parameters.Add("@Explanation", SqlDbType.NVarChar, -1).Value = (object?)entry.Explanation ?? DBNull.Value;
            command.Parameters.Add("@Url", SqlDbType.NVarChar, 2048).Value = entry.Url ?? string.Empty;
            command.Parameters.Add("@HdUrl", SqlDbType.NVarChar, 2048).Value = (object?)entry.HdUrl ?? DBNull.Value;
            command.Parameters.Add("@MediaType", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(entry.MediaType) ? "image" : entry.MediaType;
            command.Parameters.Add("@ServiceVersion", SqlDbType.NVarChar, 50).Value = (object?)entry.ServiceVersion ?? DBNull.Value;
            command.Parameters.Add("@Copyright", SqlDbType.NVarChar, 255).Value = (object?)Truncate(entry.Copyright, 255) ?? DBNull.Value;
            command.Parameters.Add("@ThumbnailUrl", SqlDbType.NVarChar, 2048).Value = (object?)entry.ThumbnailUrl ?? DBNull.Value;

            var result = await command.ExecuteScalarAsync();
            if (result != null && Convert.ToInt32(result) == 1)
            {
                inserted++;
            }
            else
            {
                duplicates++;
            }
        }

        return (inserted, duplicates);
    }

    /// <summary>
    /// Retrieves all saved APOD records from the database using ADO.NET and maps them into domain objects.
    /// Supports keyword search, media type filter, and date ordering.
    /// </summary>
    public async Task<List<ApodEntry>> GetAllEntriesAsync(string? searchTerm = null, string? mediaType = null, string? sortOrder = "desc")
    {
        var entries = new List<ApodEntry>();

        const string sql = @"
            SELECT 
                [Id],
                [Date],
                [Title],
                [Explanation],
                [Url],
                [HdUrl],
                [MediaType],
                [ServiceVersion],
                [Copyright],
                [ThumbnailUrl],
                [SavedAt]
            FROM [dbo].[ApodEntries]
            WHERE 
                (@SearchTerm IS NULL OR [Title] LIKE @SearchPattern OR [Explanation] LIKE @SearchPattern)
                AND (@MediaType IS NULL OR @MediaType = 'all' OR LOWER([MediaType]) = LOWER(@MediaType))
            ORDER BY 
                CASE WHEN @SortOrder = 'asc' THEN [Date] END ASC,
                CASE WHEN @SortOrder <> 'asc' THEN [Date] END DESC;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);

        var hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
        command.Parameters.Add("@SearchTerm", SqlDbType.NVarChar, 200).Value = hasSearch ? (object)searchTerm!.Trim() : DBNull.Value;
        command.Parameters.Add("@SearchPattern", SqlDbType.NVarChar, 202).Value = hasSearch ? (object)$"%{searchTerm!.Trim()}%" : DBNull.Value;

        var normalizedMediaType = string.IsNullOrWhiteSpace(mediaType) ? "all" : mediaType.Trim().ToLowerInvariant();
        command.Parameters.Add("@MediaType", SqlDbType.NVarChar, 50).Value = normalizedMediaType;

        var normalizedSort = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        command.Parameters.Add("@SortOrder", SqlDbType.NVarChar, 10).Value = normalizedSort;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(MapFromReader(reader));
        }

        return entries;
    }

    /// <summary>
    /// Retrieves a single APOD record by its primary key ID using ADO.NET.
    /// </summary>
    public async Task<ApodEntry?> GetEntryByIdAsync(int id)
    {
        const string sql = @"
            SELECT 
                [Id],
                [Date],
                [Title],
                [Explanation],
                [Url],
                [HdUrl],
                [MediaType],
                [ServiceVersion],
                [Copyright],
                [ThumbnailUrl],
                [SavedAt]
            FROM [dbo].[ApodEntries]
            WHERE [Id] = @Id;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapFromReader(reader);
        }

        return null;
    }

    /// <summary>
    /// Gets total count of saved entries in the database.
    /// </summary>
    public async Task<int> GetTotalCountAsync()
    {
        const string sql = "SELECT COUNT(1) FROM [dbo].[ApodEntries];";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    /// <summary>
    /// Maps a row from SqlDataReader into an ApodEntry object.
    /// </summary>
    private static ApodEntry MapFromReader(SqlDataReader reader)
    {
        return new ApodEntry
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Date = reader.GetDateTime(reader.GetOrdinal("Date")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Explanation = reader.IsDBNull(reader.GetOrdinal("Explanation")) ? null : reader.GetString(reader.GetOrdinal("Explanation")),
            Url = reader.GetString(reader.GetOrdinal("Url")),
            HdUrl = reader.IsDBNull(reader.GetOrdinal("HdUrl")) ? null : reader.GetString(reader.GetOrdinal("HdUrl")),
            MediaType = reader.GetString(reader.GetOrdinal("MediaType")),
            ServiceVersion = reader.IsDBNull(reader.GetOrdinal("ServiceVersion")) ? null : reader.GetString(reader.GetOrdinal("ServiceVersion")),
            Copyright = reader.IsDBNull(reader.GetOrdinal("Copyright")) ? null : reader.GetString(reader.GetOrdinal("Copyright")),
            ThumbnailUrl = reader.IsDBNull(reader.GetOrdinal("ThumbnailUrl")) ? null : reader.GetString(reader.GetOrdinal("ThumbnailUrl")),
            SavedAt = reader.GetDateTime(reader.GetOrdinal("SavedAt"))
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

