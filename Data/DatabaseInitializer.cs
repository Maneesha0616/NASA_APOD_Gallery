using Microsoft.Data.SqlClient;

namespace NasaApodGallery.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Ensures that the database and table exist at startup.
    /// This allows anyone running the project with LocalDB or SQL Server to start immediately.
    /// </summary>
    public static async Task InitializeDatabaseAsync(IConfiguration configuration, ILogger logger)
    {
        var targetConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        var builder = new SqlConnectionStringBuilder(targetConnectionString);
        var targetDatabase = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            targetDatabase = "NasaApodDb";
        }

        try
        {
            // Step 1: Ensure database exists by connecting to 'master'
            var masterBuilder = new SqlConnectionStringBuilder(targetConnectionString)
            {
                InitialCatalog = "master"
            };

            await using (var masterConn = new SqlConnection(masterBuilder.ConnectionString))
            {
                await masterConn.OpenAsync();
                var checkDbCmd = masterConn.CreateCommand();
                checkDbCmd.CommandText = $@"
                    IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{targetDatabase}')
                    BEGIN
                        CREATE DATABASE [{targetDatabase}];
                    END";
                await checkDbCmd.ExecuteNonQueryAsync();
            }

            // Step 2: Ensure table and index exist in target database
            await using (var targetConn = new SqlConnection(targetConnectionString))
            {
                await targetConn.OpenAsync();

                const string createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'ApodEntries' AND type = 'U')
                    BEGIN
                        CREATE TABLE [dbo].[ApodEntries] (
                            [Id]             INT IDENTITY(1,1) NOT NULL,
                            [Date]           DATE NOT NULL,
                            [Title]          NVARCHAR(500) NOT NULL,
                            [Explanation]    NVARCHAR(MAX) NULL,
                            [Url]            NVARCHAR(2048) NOT NULL,
                            [HdUrl]          NVARCHAR(2048) NULL,
                            [MediaType]      NVARCHAR(50) NOT NULL,
                            [ServiceVersion] NVARCHAR(50) NULL,
                            [Copyright]      NVARCHAR(255) NULL,
                            [ThumbnailUrl]   NVARCHAR(2048) NULL,
                            [SavedAt]        DATETIME2 NOT NULL CONSTRAINT [DF_ApodEntries_SavedAt] DEFAULT (SYSUTCDATETIME()),

                            CONSTRAINT [PK_ApodEntries] PRIMARY KEY CLUSTERED ([Id] ASC),
                            CONSTRAINT [UQ_ApodEntries_Date] UNIQUE NONCLUSTERED ([Date] ASC)
                        );
                    END;

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ApodEntries_Date' AND object_id = OBJECT_ID(N'[dbo].[ApodEntries]'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX [IX_ApodEntries_Date]
                        ON [dbo].[ApodEntries] ([Date] DESC);
                    END;";

                await using var tableCmd = new SqlCommand(createTableSql, targetConn);
                await tableCmd.ExecuteNonQueryAsync();
            }

            logger.LogInformation("Database and table initialization verified successfully for '{Database}'.", targetDatabase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Automatic database initialization encountered an issue. Please ensure SQL Server is running or run Database/CreateDatabase.sql manually.");
        }
    }
}

