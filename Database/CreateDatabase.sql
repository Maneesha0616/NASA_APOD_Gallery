-- Database Creation Script for NASA APOD Gallery Application

-- 1. Create Database if it does not already exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'NasaApodDb')
BEGIN
    CREATE DATABASE [NasaApodDb];
    PRINT 'Database [NasaApodDb] created successfully.';
END
ELSE
BEGIN
    PRINT 'Database [NasaApodDb] already exists.';
END
GO

USE [NasaApodDb];
GO

-- 2. Create ApodEntries Table 
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

    PRINT 'Table [dbo].[ApodEntries] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[ApodEntries] already exists.';
END
GO

-- 3. Create Index on Date for faster sorting and querying
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ApodEntries_Date' AND object_id = OBJECT_ID(N'[dbo].[ApodEntries]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ApodEntries_Date]
    ON [dbo].[ApodEntries] ([Date] DESC);
    PRINT 'Index [IX_ApodEntries_Date] created successfully.';
END
GO

